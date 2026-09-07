// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Shared.Cloud;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using System.Net.Http;

namespace Files.App.Services.Cloud
{
	/// <summary>
	/// Owns the OpenTelemetry providers that export <see cref="CloudTelemetryAttributes.ActivitySourceName"/> spans and
	/// <see cref="CloudTelemetryAttributes.MeterName"/> metrics over OTLP. This is the only type that references the exporter SDK.
	/// Export is fire-and-forget: an absent or unreachable collector never affects the application.
	/// </summary>
	internal sealed class CloudTelemetryExportHost : IDisposable
	{
		private const string ServiceName = "files-cloud-guard";
		private const int ExportTimeoutMs = 2_000;
		private const int MetricExportIntervalMs = 10_000;

		private readonly ICloudOptimizationSettingsService _settings;
		private readonly ILogger<CloudTelemetryExportHost> _logger;
		private readonly string _sessionId = Guid.NewGuid().ToString("N");
		private TracerProvider? _tracerProvider;
		private MeterProvider? _meterProvider;

		public CloudTelemetryExportHost(ICloudOptimizationSettingsService settings, ILogger<CloudTelemetryExportHost> logger)
		{
			_settings = settings;
			_logger = logger;
		}

		/// <summary>
		/// Starts export when the mode is not Off and telemetry is enabled. Safe to call once at startup.
		/// </summary>
		public void Start()
		{
			if (_settings.Mode == CloudOptimizationMode.Off || !_settings.TelemetryEnabled)
				return;

			if (!Uri.TryCreate(_settings.TelemetryEndpoint, UriKind.Absolute, out var endpoint) || !IsPermittedEndpoint(endpoint))
			{
				// Plaintext export never leaves the machine; see docs/privacy-and-telemetry.md.
				_logger.LogWarning("Cloud telemetry export skipped: endpoint is missing, or is neither loopback nor https.");
				return;
			}

			// The OTLP/HTTP paths are appended by the SDK only for env-var configured endpoints
			var baseEndpoint = endpoint.AbsolutePath.EndsWith('/') ? endpoint : new Uri(endpoint, endpoint.AbsolutePath + "/");
			var token = _settings.TelemetryAuthToken;

			try
			{
				var version = AppLifecycleHelper.AppVersion;
				var resource = ResourceBuilder.CreateEmpty()
					.AddService(ServiceName, serviceVersion: $"{version.Major}.{version.Minor}.{version.Build}", serviceInstanceId: _sessionId)
					.AddAttributes(
					[
						new("app.version", version.ToString()),
						new("session.id", _sessionId),
						new("installation.id", _settings.InstallationId),
					]);

				void ConfigureOtlp(OtlpExporterOptions options, string signalPath)
				{
					options.Protocol = OtlpExportProtocol.HttpProtobuf;
					options.Endpoint = new Uri(baseEndpoint, signalPath);
					options.TimeoutMilliseconds = ExportTimeoutMs;
					if (!string.IsNullOrEmpty(token))
						options.Headers = $"Authorization=Bearer {token}";

					// The endpoint check covers only the configured URL; never follow a redirect off it
					options.HttpClientFactory = () => new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false })
					{
						Timeout = TimeSpan.FromMilliseconds(ExportTimeoutMs),
					};
				}

				_tracerProvider = Sdk.CreateTracerProviderBuilder()
					.SetResourceBuilder(resource)
					.AddSource(CloudTelemetryAttributes.ActivitySourceName)
					.AddOtlpExporter(options =>
					{
						ConfigureOtlp(options, "v1/traces");
						options.ExportProcessorType = ExportProcessorType.Batch;
						options.BatchExportProcessorOptions.MaxQueueSize = 2_048;
						options.BatchExportProcessorOptions.ExporterTimeoutMilliseconds = ExportTimeoutMs;
					})
					.Build();

				_meterProvider = Sdk.CreateMeterProviderBuilder()
					.SetResourceBuilder(resource)
					.AddMeter(CloudTelemetryAttributes.MeterName)
					.AddOtlpExporter((options, readerOptions) =>
					{
						ConfigureOtlp(options, "v1/metrics");
						readerOptions.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = MetricExportIntervalMs;
						readerOptions.PeriodicExportingMetricReaderOptions.ExportTimeoutMilliseconds = ExportTimeoutMs;
					})
					.Build();

				_logger.LogInformation("Cloud telemetry export started in {Mode} mode.", _settings.Mode);
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Cloud telemetry export failed to start.");
				Dispose();
			}
		}

		/// <summary>
		/// Plaintext is allowed only on loopback; anything that crosses a network interface must be TLS.
		/// </summary>
		private static bool IsPermittedEndpoint(Uri endpoint)
			=> endpoint.IsLoopback || endpoint.Scheme == Uri.UriSchemeHttps;

		public void Dispose()
		{
			_tracerProvider?.Dispose();
			_meterProvider?.Dispose();
			_tracerProvider = null;
			_meterProvider = null;
		}
	}
}
