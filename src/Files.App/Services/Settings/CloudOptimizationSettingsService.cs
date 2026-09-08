// Copyright (c) Files Community
// Licensed under the MIT License.

using Files.Shared.Cloud;

namespace Files.App.Services.Settings
{
	internal sealed partial class CloudOptimizationSettingsService : BaseObservableJsonSettings, ICloudOptimizationSettingsService
	{
		private const string ModeEnvironmentVariable = "FILES_CLOUD_GUARD_MODE";
		private const string TokenEnvironmentVariable = "FILES_CLOUD_GUARD_TOKEN";
		private const string DefaultTelemetryEndpoint = "http://localhost:4318";
		private const string TokenVaultResource = "Files.CloudGuard.Telemetry";
		private const string TokenVaultUser = "otlp";

		// Replaced at packaging time by .github/scripts/Configure-AppxManifest.ps1. An unreplaced value still ends
		// with the placeholder suffix and is ignored, so local builds keep the loopback default, no token, and Off.
		private const string BuiltInTelemetryToken = "cloudguardtoken.secret";
		private const string BuiltInTelemetryEndpoint = "cloudguardendpoint.secret";
		private const string BuiltInDefaultMode = "cloudguardmode.secret";

		private static bool IsUnreplaced(string value)
			=> string.IsNullOrEmpty(value) || value.EndsWith(".secret", StringComparison.Ordinal);

		private static readonly CloudOptimizationMode? EnvironmentModeOverride =
			Enum.TryParse<CloudOptimizationMode>(Environment.GetEnvironmentVariable(ModeEnvironmentVariable), ignoreCase: true, out var mode)
			&& Enum.IsDefined(mode) ? mode : null;

		public CloudOptimizationSettingsService(ISettingsSharingContext settingsSharingContext)
		{
			RegisterSettingsContext(settingsSharingContext);
		}

		/// <inheritdoc/>
		public CloudOptimizationMode Mode
		{
			get => EnvironmentModeOverride ?? Get(BuiltInDefaultModeValue);
			set => Set(value);
		}

		private static CloudOptimizationMode BuiltInDefaultModeValue =>
			!IsUnreplaced(BuiltInDefaultMode)
			&& Enum.TryParse<CloudOptimizationMode>(BuiltInDefaultMode, ignoreCase: true, out var builtIn)
			&& Enum.IsDefined(builtIn)
				? builtIn
				: CloudOptimizationMode.Off;

		/// <inheritdoc/>
		public bool TelemetryEnabled
		{
			get => Get(true);
			set => Set(value);
		}

		/// <inheritdoc/>
		public string TelemetryEndpoint
		{
			get
			{
				var fallback = IsUnreplaced(BuiltInTelemetryEndpoint) ? DefaultTelemetryEndpoint : BuiltInTelemetryEndpoint;
				return Get(fallback) ?? fallback;
			}
			set => Set(value);
		}

		/// <inheritdoc/>
		public string TelemetryAuthToken
		{
			get
			{
				var environmentToken = Environment.GetEnvironmentVariable(TokenEnvironmentVariable);
				if (!string.IsNullOrEmpty(environmentToken))
					return environmentToken;

				var vaultToken = CredentialsHelpers.GetPassword(TokenVaultResource, TokenVaultUser);
				if (!string.IsNullOrEmpty(vaultToken))
					return vaultToken;

				return IsUnreplaced(BuiltInTelemetryToken) ? string.Empty : BuiltInTelemetryToken;
			}
			set
			{
				if (!string.IsNullOrEmpty(CredentialsHelpers.GetPassword(TokenVaultResource, TokenVaultUser)))
					CredentialsHelpers.DeleteSavedPassword(TokenVaultResource, TokenVaultUser);

				if (!string.IsNullOrEmpty(value))
					CredentialsHelpers.SavePassword(TokenVaultResource, TokenVaultUser, value);
			}
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
