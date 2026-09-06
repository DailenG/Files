// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Shared.Cloud
{
	/// <summary>
	/// Operating mode for provider-aware cloud optimizations.
	/// </summary>
	public enum CloudOptimizationMode
	{
		/// <summary>
		/// Stock behavior; cloud telemetry disabled.
		/// </summary>
		Off,

		/// <summary>
		/// Stock behavior; cloud interactions are recorded.
		/// </summary>
		Observe,

		/// <summary>
		/// Provider-aware protections active; interactions and policy decisions are recorded.
		/// </summary>
		Protect
	}
}
