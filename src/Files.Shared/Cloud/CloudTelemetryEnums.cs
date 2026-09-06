// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Shared.Cloud
{
	/// <summary>
	/// Files-owned operation performed against a storage location.
	/// </summary>
	public enum CloudOperationName
	{
		Navigate,
		EnumerateDirectory,
		RequestThumbnail,
		RequestPreview,
		ReadBasicMetadata,
		ReadRichProperties,
		Search,
		CalculateFolderSize,
		ResolveShortcut,
		BuildShellMenu,
		OpenFile
	}

	/// <summary>
	/// What triggered the operation.
	/// </summary>
	public enum CloudAccessOrigin
	{
		Unknown,
		Navigation,
		FolderDisplay,
		VisibleItem,
		SelectionChanged,
		Hover,
		Background,
		RestoredTab,
		ContextMenu,
		ExplicitButton,
		DoubleClick,
		KeyboardOpen
	}

	/// <summary>
	/// Policy applied to an operation.
	/// </summary>
	public enum CloudPolicyDecisionKind
	{
		NotApplicable,
		Allowed,
		Observed,
		CachedOnly,
		Deferred,
		GenericFallback,
		Redirected,
		Blocked
	}

	/// <summary>
	/// Terminal outcome of an operation.
	/// </summary>
	public enum CloudOperationOutcome
	{
		Success,
		Cancelled,
		Failure
	}

	/// <summary>
	/// Result of a thumbnail request.
	/// </summary>
	public enum CloudThumbnailResult
	{
		NotApplicable,
		CacheHit,
		CacheMiss,
		GenericFallback
	}

	/// <summary>
	/// Result of a preview request.
	/// </summary>
	public enum CloudPreviewResult
	{
		NotApplicable,
		DirectLoad,
		Deferred,
		ExplicitLoad,
		Cancelled
	}

	/// <summary>
	/// Coarse error category; never carries messages or paths.
	/// </summary>
	public enum CloudErrorCategory
	{
		None,
		Cancelled,
		NotFound,
		AccessDenied,
		IO,
		Timeout,
		Unknown
	}
}
