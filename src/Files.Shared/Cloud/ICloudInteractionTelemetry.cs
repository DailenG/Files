// Copyright (c) Files Community
// Licensed under the MIT License.

using System;

namespace Files.Shared.Cloud
{
	/// <summary>
	/// Records Files-owned interactions with cloud-backed locations. Implementations must never block or throw.
	/// </summary>
	public interface ICloudInteractionTelemetry
	{
		/// <summary>
		/// Gets the currently effective operating mode.
		/// </summary>
		CloudOptimizationMode Mode { get; }

		/// <summary>
		/// Starts a trace span for the operation. Returns null when telemetry is inactive or no listener is attached.
		/// </summary>
		IDisposable? StartOperation(CloudInteractionOperation operation);

		/// <summary>
		/// Records a policy decision.
		/// </summary>
		void RecordDecision(CloudPolicyDecision decision);

		/// <summary>
		/// Records the terminal result of an operation.
		/// </summary>
		void RecordResult(CloudInteractionResult result);
	}
}
