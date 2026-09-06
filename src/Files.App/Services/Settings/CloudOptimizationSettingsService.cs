// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Shared.Cloud;

namespace Files.App.Services.Settings
{
	internal sealed partial class CloudOptimizationSettingsService : BaseObservableJsonSettings, ICloudOptimizationSettingsService
	{
		private const string ModeEnvironmentVariable = "FILES_CLOUD_GUARD_MODE";
		private const string DefaultTelemetryEndpoint = "http://localhost:4318";

		private static readonly CloudOptimizationMode? EnvironmentModeOverride =
			Enum.TryParse<CloudOptimizationMode>(Environment.GetEnvironmentVariable(ModeEnvironmentVariable), ignoreCase: true, out var mode) ? mode : null;

		public CloudOptimizationSettingsService(ISettingsSharingContext settingsSharingContext)
		{
			RegisterSettingsContext(settingsSharingContext);
		}

		/// <inheritdoc/>
		public CloudOptimizationMode Mode
		{
			get => EnvironmentModeOverride ?? Get(CloudOptimizationMode.Off);
			set => Set(value);
		}

		/// <inheritdoc/>
		public bool TelemetryEnabled
		{
			get => Get(false);
			set => Set(value);
		}

		/// <inheritdoc/>
		public string TelemetryEndpoint
		{
			get => Get(DefaultTelemetryEndpoint) ?? DefaultTelemetryEndpoint;
			set => Set(value);
		}

		/// <inheritdoc/>
		public List<string> EgnyteConfiguredRoots
		{
			get => Get<List<string>>([]) ?? [];
			set => Set(value);
		}

		/// <inheritdoc/>
		public string InstallationId
		{
			get
			{
				var id = Get(string.Empty);
				if (string.IsNullOrEmpty(id))
				{
					id = Guid.NewGuid().ToString("N");
					Set(id);
				}

				return id;
			}
		}
	}
}
