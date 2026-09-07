// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Shared.Cloud;

namespace Files.App.Data.Contracts
{
	/// <summary>
	/// Settings for provider-aware cloud optimizations and cloud-interaction telemetry.
	/// </summary>
	public interface ICloudOptimizationSettingsService : IBaseSettingsService, INotifyPropertyChanged
	{
		/// <summary>
		/// Gets or sets the operating mode. The FILES_CLOUD_GUARD_MODE environment variable overrides this value for the process lifetime.
		/// </summary>
		CloudOptimizationMode Mode { get; set; }

		/// <summary>
		/// Gets or sets whether telemetry is exported to the configured collector. Has no effect when <see cref="Mode"/> is Off.
		/// </summary>
		bool TelemetryEnabled { get; set; }

		/// <summary>
		/// Gets or sets the OTLP/HTTP base endpoint telemetry is exported to. Plaintext http is accepted only for loopback; remote endpoints must be https.
		/// </summary>
		string TelemetryEndpoint { get; set; }

		/// <summary>
		/// Gets or sets the bearer token sent with every export request. Stored in the Windows credential vault, not in user_settings.json.
		/// The FILES_CLOUD_GUARD_TOKEN environment variable overrides this value for the process lifetime. Empty means no Authorization header.
		/// </summary>
		string TelemetryAuthToken { get; set; }

		/// <summary>
		/// Gets or sets administrator-configured roots (drive letters or UNC roots) to treat as Egnyte. Empty means automatic detection only.
		/// </summary>
		List<string> EgnyteConfiguredRoots { get; set; }

		/// <summary>
		/// Gets a random per-installation identifier used in place of any machine or user identity.
		/// </summary>
		string InstallationId { get; }
	}
}
