// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Shared.Cloud
{
	/// <summary>
	/// Represents the classification result and policy context for a given filesystem path.
	/// </summary>
	public sealed record CloudLocationContext(
		CloudLocationKind Kind,
		string RootIdentifier,
		bool IsCloudBacked,
		bool HasHydrationRisk)
	{
		/// <summary>
		/// Gets a default context representing a local disk location with no hydration risk.
		/// </summary>
		public static CloudLocationContext Local { get; } = new(CloudLocationKind.Local, "local", false, false);

		/// <summary>
		/// Gets a default context representing a standard network share with no cloud hydration risk.
		/// </summary>
		public static CloudLocationContext StandardNetwork { get; } = new(CloudLocationKind.StandardNetwork, "network", false, false);

		/// <summary>
		/// Gets a default context representing an unknown location.
		/// </summary>
		public static CloudLocationContext Unknown { get; } = new(CloudLocationKind.Unknown, "unknown", false, false);
	}
}
