// Copyright (c) Files Community
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Files.Shared.Cloud
{
	/// <summary>
	/// Default <see cref="ICloudInteractionTelemetry"/> built on <see cref="ActivitySource"/> and <see cref="Meter"/>.
	/// Records only cloud-backed locations, is a no-op in <see cref="CloudOptimizationMode.Off"/>, and never throws.
	/// </summary>
	public sealed class CloudInteractionTelemetry : ICloudInteractionTelemetry, IDisposable
	{
		private readonly Func<CloudOptimizationMode> _modeProvider;
		private readonly ILogger? _logger;
		private readonly ActivitySource _activitySource = new(CloudTelemetryAttributes.ActivitySourceName);
		private readonly Meter _meter = new(CloudTelemetryAttributes.MeterName);
		private readonly Counter<long> _operations;
		private readonly Histogram<double> _duration;
		private readonly Counter<long> _decisions;
		private readonly Counter<long> _thumbnailCacheHits;
		private readonly Counter<long> _thumbnailCacheMisses;
		private readonly Counter<long> _thumbnailGenericFallbacks;
		private readonly Counter<long> _previewDeferred;
		private readonly Counter<long> _previewExplicitLoads;
		private readonly Counter<long> _failures;
		private int _internalErrorsLogged;

		public CloudInteractionTelemetry(Func<CloudOptimizationMode> modeProvider, ILogger? logger = null)
		{
			_modeProvider = modeProvider;
			_logger = logger;
			_operations = _meter.CreateCounter<long>("files.cloud.operations", "{operation}");
			_duration = _meter.CreateHistogram<double>("files.cloud.operation.duration", "ms");
			_decisions = _meter.CreateCounter<long>("files.cloud.policy.decisions", "{decision}");
			_thumbnailCacheHits = _meter.CreateCounter<long>("files.cloud.thumbnail.cache_hits", "{thumbnail}");
			_thumbnailCacheMisses = _meter.CreateCounter<long>("files.cloud.thumbnail.cache_misses", "{thumbnail}");
			_thumbnailGenericFallbacks = _meter.CreateCounter<long>("files.cloud.thumbnail.generic_fallbacks", "{thumbnail}");
			_previewDeferred = _meter.CreateCounter<long>("files.cloud.preview.deferred", "{preview}");
			_previewExplicitLoads = _meter.CreateCounter<long>("files.cloud.preview.explicit_loads", "{preview}");
			_failures = _meter.CreateCounter<long>("files.cloud.failures", "{operation}");
		}

		/// <inheritdoc/>
		public CloudOptimizationMode Mode
		{
			get
			{
				try
				{
					return _modeProvider();
				}
				catch
				{
					return CloudOptimizationMode.Off;
				}
			}
		}

		/// <inheritdoc/>
		public IDisposable? StartOperation(CloudInteractionOperation operation)
		{
			if (!ShouldRecord(operation, out var mode))
				return null;

			try
			{
				var activity = _activitySource.StartActivity(CloudTelemetryAttributes.OperationSpanName);
				if (activity is null)
					return null;

				foreach (var (key, value) in BaseTags(operation, mode))
					activity.SetTag(key, value);

				return activity;
			}
			catch (Exception ex)
			{
				LogInternalError(ex);
				return null;
			}
		}

		/// <inheritdoc/>
		public void RecordDecision(CloudPolicyDecision decision)
		{
			if (!ShouldRecord(decision.Operation, out var mode))
				return;

			try
			{
				var tags = BaseTags(decision.Operation, mode);
				tags.Add(CloudTelemetryAttributes.PolicyDecision, CloudTelemetryAttributes.Decision(decision.Decision));
				_decisions.Add(1, tags);
				Activity.Current?.SetTag(CloudTelemetryAttributes.PolicyDecision, CloudTelemetryAttributes.Decision(decision.Decision));
			}
			catch (Exception ex)
			{
				LogInternalError(ex);
			}
		}

		/// <inheritdoc/>
		public void RecordResult(CloudInteractionResult result)
		{
			if (!ShouldRecord(result.Operation, out var mode))
				return;

			try
			{
				var tags = BaseTags(result.Operation, mode);
				tags.Add(CloudTelemetryAttributes.OperationOutcome, CloudTelemetryAttributes.Outcome(result.Outcome));
				tags.Add(CloudTelemetryAttributes.ThumbnailResult, CloudTelemetryAttributes.Thumbnail(result.Thumbnail));
				tags.Add(CloudTelemetryAttributes.PreviewResult, CloudTelemetryAttributes.Preview(result.Preview));
				tags.Add(CloudTelemetryAttributes.ErrorCategory, CloudTelemetryAttributes.Error(result.Error));

				_operations.Add(1, tags);
				_duration.Record(result.Duration.TotalMilliseconds, tags);

				if (result.Outcome == CloudOperationOutcome.Failure)
					_failures.Add(1, tags);

				switch (result.Thumbnail)
				{
					case CloudThumbnailResult.CacheHit: _thumbnailCacheHits.Add(1, tags); break;
					case CloudThumbnailResult.CacheMiss: _thumbnailCacheMisses.Add(1, tags); break;
					case CloudThumbnailResult.GenericFallback: _thumbnailGenericFallbacks.Add(1, tags); break;
				}

				switch (result.Preview)
				{
					case CloudPreviewResult.Deferred: _previewDeferred.Add(1, tags); break;
					case CloudPreviewResult.ExplicitLoad: _previewExplicitLoads.Add(1, tags); break;
				}

				if (Activity.Current is { } activity)
				{
					foreach (var (key, value) in tags)
						activity.SetTag(key, value);

					if (result.Outcome == CloudOperationOutcome.Failure)
						activity.SetStatus(ActivityStatusCode.Error);
				}
			}
			catch (Exception ex)
			{
				LogInternalError(ex);
			}
		}

		public void Dispose()
		{
			_activitySource.Dispose();
			_meter.Dispose();
		}

		private bool ShouldRecord(CloudInteractionOperation operation, out CloudOptimizationMode mode)
		{
			mode = Mode;
			return mode != CloudOptimizationMode.Off && operation.Location.IsCloudBacked;
		}

		private static TagList BaseTags(CloudInteractionOperation operation, CloudOptimizationMode mode)
		{
			var tags = new TagList
			{
				{ CloudTelemetryAttributes.ProviderKind, CloudTelemetryAttributes.Provider(operation.Location.Kind) },
				{ CloudTelemetryAttributes.OptimizationMode, CloudTelemetryAttributes.Mode(mode) },
				{ CloudTelemetryAttributes.OperationName, CloudTelemetryAttributes.Operation(operation.Name) },
				{ CloudTelemetryAttributes.AccessOrigin, CloudTelemetryAttributes.Origin(operation.Origin) },
				{ CloudTelemetryAttributes.AccessIsExplicit, operation.IsExplicit },
			};

			if (operation.ItemCount is not null)
				tags.Add(CloudTelemetryAttributes.ItemCountBucket, CloudTelemetryAttributes.ItemCount(operation.ItemCount));
			if (operation.FileSize is not null)
				tags.Add(CloudTelemetryAttributes.FileSizeBucket, CloudTelemetryAttributes.FileSize(operation.FileSize));
			if (operation.Extension is not null)
				tags.Add(CloudTelemetryAttributes.ExtensionGroup, CloudTelemetryAttributes.Extension(operation.Extension));

			return tags;
		}

		private void LogInternalError(Exception ex)
		{
			// Rate-limit: telemetry problems must never flood the log.
			if (_internalErrorsLogged < 5 && System.Threading.Interlocked.Increment(ref _internalErrorsLogged) <= 5)
				_logger?.LogWarning("Cloud telemetry recording failed: {ExceptionType}", ex.GetType().Name);
		}
	}
}
