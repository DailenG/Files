// Copyright (c) Files Community
// Licensed under the MIT License.

using System;

namespace Files.Shared.Cloud
{
	/// <summary>
	/// Describes a Files-owned operation against a classified location. Carries only bounded, privacy-safe data.
	/// </summary>
	/// <param name="Name">Operation performed.</param>
	/// <param name="Origin">What triggered the operation.</param>
	/// <param name="Location">Classification of the target location.</param>
	/// <param name="IsExplicit">True when the user deliberately requested the operation.</param>
	/// <param name="ItemCount">Optional item count; exported only as a bucket.</param>
	/// <param name="FileSize">Optional file size in bytes; exported only as a bucket.</param>
	/// <param name="Extension">Optional file extension; exported only as a group.</param>
	public sealed record CloudInteractionOperation(
		CloudOperationName Name,
		CloudAccessOrigin Origin,
		CloudLocationContext Location,
		bool IsExplicit,
		int? ItemCount = null,
		long? FileSize = null,
		string? Extension = null);

	/// <summary>
	/// A policy decision applied to an operation.
	/// </summary>
	public sealed record CloudPolicyDecision(
		CloudInteractionOperation Operation,
		CloudPolicyDecisionKind Decision);

	/// <summary>
	/// Terminal result of an operation.
	/// </summary>
	public sealed record CloudInteractionResult(
		CloudInteractionOperation Operation,
		CloudOperationOutcome Outcome,
		TimeSpan Duration,
		CloudThumbnailResult Thumbnail = CloudThumbnailResult.NotApplicable,
		CloudPreviewResult Preview = CloudPreviewResult.NotApplicable,
		CloudErrorCategory Error = CloudErrorCategory.None);
}
