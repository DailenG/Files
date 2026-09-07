// Copyright (c) Files Community
// Licensed under the MIT License.

#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using Files.Shared.Cloud;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Files.Shared.Tests.Cloud
{
	[TestClass]
	public class CloudInteractionTelemetryTests
	{
		private static readonly CloudLocationContext Egnyte = new(CloudLocationKind.Egnyte, "egnyte", true, true);

		private sealed class Capture : IDisposable
		{
			public readonly List<(string Instrument, long Value, Dictionary<string, object?> Tags)> Measurements = [];
			public readonly List<Activity> Activities = [];
			private readonly MeterListener _meterListener = new();
			private readonly ActivityListener _activityListener;

			public Capture()
			{
				_meterListener.InstrumentPublished = (instrument, listener) =>
				{
					if (instrument.Meter.Name == CloudTelemetryAttributes.MeterName)
						listener.EnableMeasurementEvents(instrument);
				};
				_meterListener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
					Measurements.Add((instrument.Name, value, tags.ToArray().ToDictionary(t => t.Key, t => t.Value))));
				_meterListener.Start();

				_activityListener = new ActivityListener
				{
					ShouldListenTo = source => source.Name == CloudTelemetryAttributes.ActivitySourceName,
					Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
					ActivityStopped = Activities.Add
				};
				ActivitySource.AddActivityListener(_activityListener);
			}

			public void Dispose()
			{
				_meterListener.Dispose();
				_activityListener.Dispose();
			}
		}

		[TestMethod]
		public void OffMode_RecordsNothing()
		{
			using var capture = new Capture();
			using var telemetry = new CloudInteractionTelemetry(() => CloudOptimizationMode.Off);
			var op = new CloudInteractionOperation(CloudOperationName.RequestThumbnail, CloudAccessOrigin.VisibleItem, Egnyte, false);

			using (telemetry.StartOperation(op))
				telemetry.RecordDecision(new CloudPolicyDecision(op, CloudPolicyDecisionKind.CachedOnly));
			telemetry.RecordResult(new CloudInteractionResult(op, CloudOperationOutcome.Success, TimeSpan.FromMilliseconds(5)));

			Assert.AreEqual(0, capture.Measurements.Count);
			Assert.AreEqual(0, capture.Activities.Count);
		}

		[TestMethod]
		public void ObserveMode_LocalLocation_IsNotRecorded()
		{
			using var capture = new Capture();
			using var telemetry = new CloudInteractionTelemetry(() => CloudOptimizationMode.Observe);
			var op = new CloudInteractionOperation(CloudOperationName.Navigate, CloudAccessOrigin.Navigation, CloudLocationContext.Local, false);

			Assert.IsNull(telemetry.StartOperation(op));
			telemetry.RecordResult(new CloudInteractionResult(op, CloudOperationOutcome.Success, TimeSpan.Zero));

			Assert.AreEqual(0, capture.Measurements.Count);
		}

		[TestMethod]
		public void ObserveMode_EgnyteThumbnail_EmitsSpanAndBoundedMetrics()
		{
			using var capture = new Capture();
			using var telemetry = new CloudInteractionTelemetry(() => CloudOptimizationMode.Observe);
			var op = new CloudInteractionOperation(
				CloudOperationName.RequestThumbnail, CloudAccessOrigin.VisibleItem, Egnyte, false,
				ItemCount: 250, FileSize: 20L * 1024 * 1024, Extension: ".DWG");

			using (telemetry.StartOperation(op))
			{
				telemetry.RecordDecision(new CloudPolicyDecision(op, CloudPolicyDecisionKind.Observed));
				telemetry.RecordResult(new CloudInteractionResult(op, CloudOperationOutcome.Success, TimeSpan.FromMilliseconds(12), CloudThumbnailResult.CacheMiss));
			}

			Assert.AreEqual(1, capture.Activities.Count);
			var activity = capture.Activities[0];
			Assert.AreEqual(CloudTelemetryAttributes.OperationSpanName, activity.OperationName);
			Assert.AreEqual("egnyte", activity.GetTagItem(CloudTelemetryAttributes.ProviderKind));
			Assert.AreEqual("observe", activity.GetTagItem(CloudTelemetryAttributes.OptimizationMode));
			Assert.AreEqual("101-1000", activity.GetTagItem(CloudTelemetryAttributes.ItemCountBucket));
			Assert.AreEqual("10-100MB", activity.GetTagItem(CloudTelemetryAttributes.FileSizeBucket));
			Assert.AreEqual("cad", activity.GetTagItem(CloudTelemetryAttributes.ExtensionGroup));
			Assert.AreEqual("observed", activity.GetTagItem(CloudTelemetryAttributes.PolicyDecision));
			Assert.AreEqual("cache_miss", activity.GetTagItem(CloudTelemetryAttributes.ThumbnailResult));

			var names = capture.Measurements.Select(m => m.Instrument).ToList();
			CollectionAssert.Contains(names, "files.cloud.operations");
			CollectionAssert.Contains(names, "files.cloud.policy.decisions");
			CollectionAssert.Contains(names, "files.cloud.thumbnail.cache_misses");
			CollectionAssert.DoesNotContain(names, "files.cloud.failures");

			// No raw path, name, or extension leaks into any tag value.
			foreach (var (_, _, tags) in capture.Measurements)
				foreach (var value in tags.Values.OfType<string>())
					Assert.IsFalse(value.Contains('\\') || value.Contains(".DWG", StringComparison.OrdinalIgnoreCase), $"Unexpected tag value: {value}");
		}

		[TestMethod]
		public void ProtectMode_Failure_IncrementsFailuresAndMarksSpanError()
		{
			using var capture = new Capture();
			using var telemetry = new CloudInteractionTelemetry(() => CloudOptimizationMode.Protect);
			var op = new CloudInteractionOperation(CloudOperationName.RequestPreview, CloudAccessOrigin.ExplicitButton, Egnyte, true);

			using (telemetry.StartOperation(op))
				telemetry.RecordResult(new CloudInteractionResult(op, CloudOperationOutcome.Failure, TimeSpan.FromSeconds(1), Error: CloudErrorCategory.IO));

			Assert.AreEqual(ActivityStatusCode.Error, capture.Activities.Single().Status);
			var failure = capture.Measurements.Single(m => m.Instrument == "files.cloud.failures");
			Assert.AreEqual("io", failure.Tags[CloudTelemetryAttributes.ErrorCategory]);
			Assert.AreEqual(true, failure.Tags[CloudTelemetryAttributes.AccessIsExplicit]);
		}

		[TestMethod]
		public void ModeProviderThrows_FailsSafeToOff()
		{
			using var telemetry = new CloudInteractionTelemetry(() => throw new InvalidOperationException());
			Assert.AreEqual(CloudOptimizationMode.Off, telemetry.Mode);
		}

		[TestMethod]
		public void Buckets_AreBoundedAtBoundaries()
		{
			Assert.AreEqual("0", CloudTelemetryAttributes.ItemCount(0));
			Assert.AreEqual("1-10", CloudTelemetryAttributes.ItemCount(10));
			Assert.AreEqual("11-100", CloudTelemetryAttributes.ItemCount(11));
			Assert.AreEqual("1001+", CloudTelemetryAttributes.ItemCount(int.MaxValue));
			Assert.AreEqual("unknown", CloudTelemetryAttributes.FileSize(null));
			Assert.AreEqual("<1MB", CloudTelemetryAttributes.FileSize(1024 * 1024 - 1));
			Assert.AreEqual("1-10MB", CloudTelemetryAttributes.FileSize(1024 * 1024));
			Assert.AreEqual("1GB+", CloudTelemetryAttributes.FileSize(long.MaxValue));
			Assert.AreEqual("shortcut", CloudTelemetryAttributes.Extension(".egnyte_f"));
			Assert.AreEqual("bim", CloudTelemetryAttributes.Extension("rvt"));
			Assert.AreEqual("other", CloudTelemetryAttributes.Extension(".averyveryverylongextension"));
			Assert.AreEqual(CloudErrorCategory.AccessDenied, CloudTelemetryAttributes.Categorize(new UnauthorizedAccessException("C:\\secret\\path")));
		}
	}
}
