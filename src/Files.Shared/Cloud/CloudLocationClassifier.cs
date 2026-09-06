// Copyright (c) Files Community
// Licensed under the MIT License.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Files.Shared.Cloud
{
	/// <summary>
	/// Default implementation of <see cref="ICloudLocationClassifier"/> that identifies Egnyte virtual roots,
	/// UNC paths, and configured overrides without opening file content.
	/// </summary>
	public sealed class CloudLocationClassifier : ICloudLocationClassifier
	{
		private const string EgnyteUncPrefix = @"\\egnytedrive\";
		private readonly ConcurrentDictionary<string, CloudLocationContext> _rootCache = new(StringComparer.OrdinalIgnoreCase);
		private readonly List<string> _configuredEgnyteRoots = [];
		private readonly Func<string, string?>? _remotePathResolver;

		/// <summary>
		/// Initializes a new instance of <see cref="CloudLocationClassifier"/>.
		/// </summary>
		/// <param name="configuredEgnyteRoots">Optional list of configured roots (e.g. drive letters or paths) to treat as Egnyte.</param>
		/// <param name="remotePathResolver">Optional resolver to determine UNC remote path for a mapped drive letter.</param>
		public CloudLocationClassifier(
			IEnumerable<string>? configuredEgnyteRoots = null,
			Func<string, string?>? remotePathResolver = null)
		{
			if (configuredEgnyteRoots is not null)
			{
				foreach (var root in configuredEgnyteRoots)
				{
					if (!string.IsNullOrWhiteSpace(root))
						_configuredEgnyteRoots.Add(NormalizeRoot(root));
				}
			}

			_remotePathResolver = remotePathResolver;
		}

		/// <inheritdoc/>
		public ValueTask<CloudLocationContext> ClassifyAsync(string path, CancellationToken cancellationToken = default)
		{
			if (string.IsNullOrWhiteSpace(path))
				return ValueTask.FromResult(CloudLocationContext.Unknown);

			string normalized = path.Trim();

			// 1. Check direct UNC Egnyte prefix
			if (normalized.StartsWith(EgnyteUncPrefix, StringComparison.OrdinalIgnoreCase) ||
			    normalized.Equals(@"\\egnytedrive", StringComparison.OrdinalIgnoreCase))
			{
				var egnyteContext = new CloudLocationContext(
					CloudLocationKind.Egnyte,
					"egnyte",
					IsCloudBacked: true,
					HasHydrationRisk: true);

				return ValueTask.FromResult(egnyteContext);
			}

			// Extract root component (e.g. "C:\" or "\\server\share")
			string root = GetPathRoot(normalized);
			if (string.IsNullOrEmpty(root))
			{
				return ValueTask.FromResult(CloudLocationContext.Unknown);
			}

			// 2. Check root cache
			if (_rootCache.TryGetValue(root, out var cachedContext))
			{
				return ValueTask.FromResult(cachedContext);
			}

			// 3. Check configured root overrides
			foreach (var configuredRoot in _configuredEgnyteRoots)
			{
				if (root.Equals(configuredRoot, StringComparison.OrdinalIgnoreCase) ||
				    normalized.StartsWith(configuredRoot, StringComparison.OrdinalIgnoreCase))
				{
					var egnyteContext = new CloudLocationContext(
						CloudLocationKind.Egnyte,
						"egnyte",
						IsCloudBacked: true,
						HasHydrationRisk: true);

					_rootCache[root] = egnyteContext;
					return ValueTask.FromResult(egnyteContext);
				}
			}

			// 4. Resolve mapped drive to remote UNC if resolver provided
			if (_remotePathResolver is not null && root.Length >= 2 && root[1] == ':')
			{
				string driveLetter = root[..2];
				try
				{
					string? remoteUnc = _remotePathResolver(driveLetter);
					if (!string.IsNullOrWhiteSpace(remoteUnc))
					{
						if (remoteUnc.StartsWith(EgnyteUncPrefix, StringComparison.OrdinalIgnoreCase) ||
						    remoteUnc.Equals(@"\\egnytedrive", StringComparison.OrdinalIgnoreCase))
						{
							var egnyteContext = new CloudLocationContext(
								CloudLocationKind.Egnyte,
								"egnyte",
								IsCloudBacked: true,
								HasHydrationRisk: true);

							_rootCache[root] = egnyteContext;
							return ValueTask.FromResult(egnyteContext);
						}

						var networkContext = new CloudLocationContext(
							CloudLocationKind.StandardNetwork,
							"network",
							IsCloudBacked: false,
							HasHydrationRisk: false);

						_rootCache[root] = networkContext;
						return ValueTask.FromResult(networkContext);
					}
				}
				catch
				{
					// Fail safely on resolver error
				}
			}

			// 5. Check if standard UNC network path
			if (normalized.StartsWith(@"\\", StringComparison.Ordinal))
			{
				var networkContext = new CloudLocationContext(
					CloudLocationKind.StandardNetwork,
					"network",
					IsCloudBacked: false,
					HasHydrationRisk: false);

				_rootCache[root] = networkContext;
				return ValueTask.FromResult(networkContext);
			}

			// 6. Default to local path
			if (root.Length >= 2 && root[1] == ':')
			{
				var localContext = CloudLocationContext.Local;
				_rootCache[root] = localContext;
				return ValueTask.FromResult(localContext);
			}

			return ValueTask.FromResult(CloudLocationContext.Unknown);
		}

		private static string GetPathRoot(string path)
		{
			try
			{
				string? root = Path.GetPathRoot(path);
				if (!string.IsNullOrEmpty(root))
					return NormalizeRoot(root);
			}
			catch
			{
				// In case of malformed path
			}

			return string.Empty;
		}

		private static string NormalizeRoot(string root)
		{
			return root.TrimEnd('\\', '/').ToUpperInvariant() + "\\";
		}
	}
}
