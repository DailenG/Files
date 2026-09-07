// Copyright (c) Files Community
// Licensed under the MIT License.

using System;

namespace Files.Shared.Cloud
{
	/// <summary>
	/// Attribute names and normalization for cloud telemetry. Every value produced here has bounded cardinality.
	/// </summary>
	public static class CloudTelemetryAttributes
	{
		public const string ActivitySourceName = "Files.Cloud";
		public const string MeterName = "Files.Cloud";
		public const string OperationSpanName = "files.cloud.operation";

		public const string ProviderKind = "provider.kind";
		public const string OptimizationMode = "optimization.mode";
		public const string OperationName = "operation.name";
		public const string AccessOrigin = "access.origin";
		public const string AccessIsExplicit = "access.is_explicit";
		public const string PolicyDecision = "policy.decision";
		public const string OperationOutcome = "operation.outcome";
		public const string ThumbnailResult = "thumbnail.result";
		public const string PreviewResult = "preview.result";
		public const string ItemCountBucket = "item_count_bucket";
		public const string FileSizeBucket = "file_size_bucket";
		public const string ExtensionGroup = "extension_group";
		public const string ErrorCategory = "error.category";

		private const long MB = 1024L * 1024L;
		private const long GB = 1024L * MB;

		public static string ItemCount(int? count) => count switch
		{
			null => "unknown",
			<= 0 => "0",
			<= 10 => "1-10",
			<= 100 => "11-100",
			<= 1000 => "101-1000",
			_ => "1001+"
		};

		public static string FileSize(long? bytes) => bytes switch
		{
			null or < 0 => "unknown",
			< MB => "<1MB",
			< 10 * MB => "1-10MB",
			< 100 * MB => "10-100MB",
			< GB => "100MB-1GB",
			_ => "1GB+"
		};

		public static string Extension(string? extension)
		{
			if (string.IsNullOrEmpty(extension))
				return "other";

			var ext = extension.AsSpan().TrimStart('.');
			if (ext.Length == 0 || ext.Length > 12)
				return "other";

			return ext.ToString().ToLowerInvariant() switch
			{
				"doc" or "docx" or "xls" or "xlsx" or "ppt" or "pptx" or "one" or "vsd" or "vsdx" or "odt" or "ods" or "odp" => "office",
				"pdf" => "pdf",
				"jpg" or "jpeg" or "png" or "gif" or "bmp" or "tif" or "tiff" or "webp" or "heic" or "svg" or "psd" => "image",
				"mp4" or "mov" or "avi" or "mkv" or "wmv" or "webm" or "m4v" => "video",
				"mp3" or "wav" or "flac" or "aac" or "m4a" or "wma" or "ogg" => "audio",
				"dwg" or "dxf" or "dgn" or "skp" or "3ds" or "step" or "stp" or "iges" or "igs" => "cad",
				"rvt" or "rfa" or "rte" or "nwd" or "nwc" or "nwf" or "ifc" => "bim",
				"zip" or "7z" or "rar" or "tar" or "gz" or "iso" or "cab" => "archive",
				"txt" or "md" or "log" or "csv" or "rtf" => "text",
				"cs" or "js" or "ts" or "py" or "json" or "xml" or "yaml" or "yml" or "ps1" or "html" or "css" => "code",
				"lnk" or "url" or "egnyte_f" or "egnyte_d" => "shortcut",
				_ => "other"
			};
		}

		public static string Provider(CloudLocationKind kind) => kind switch
		{
			CloudLocationKind.Local => "local",
			CloudLocationKind.StandardNetwork => "standard_network",
			CloudLocationKind.Egnyte => "egnyte",
			CloudLocationKind.OtherCloudOrVirtual => "other_cloud",
			_ => "unknown"
		};

		public static string Mode(CloudOptimizationMode mode) => mode switch
		{
			CloudOptimizationMode.Observe => "observe",
			CloudOptimizationMode.Protect => "protect",
			_ => "off"
		};

		public static string Operation(CloudOperationName name) => name switch
		{
			CloudOperationName.Navigate => "navigate",
			CloudOperationName.EnumerateDirectory => "enumerate_directory",
			CloudOperationName.RequestThumbnail => "request_thumbnail",
			CloudOperationName.RequestPreview => "request_preview",
			CloudOperationName.ReadBasicMetadata => "read_basic_metadata",
			CloudOperationName.ReadRichProperties => "read_rich_properties",
			CloudOperationName.Search => "search",
			CloudOperationName.CalculateFolderSize => "calculate_folder_size",
			CloudOperationName.ResolveShortcut => "resolve_shortcut",
			CloudOperationName.BuildShellMenu => "build_shell_menu",
			CloudOperationName.OpenFile => "open_file",
			_ => "unknown"
		};

		public static string Origin(CloudAccessOrigin origin) => origin switch
		{
			CloudAccessOrigin.Navigation => "navigation",
			CloudAccessOrigin.FolderDisplay => "folder_display",
			CloudAccessOrigin.VisibleItem => "visible_item",
			CloudAccessOrigin.SelectionChanged => "selection_changed",
			CloudAccessOrigin.Hover => "hover",
			CloudAccessOrigin.Background => "background",
			CloudAccessOrigin.RestoredTab => "restored_tab",
			CloudAccessOrigin.ContextMenu => "context_menu",
			CloudAccessOrigin.ExplicitButton => "explicit_button",
			CloudAccessOrigin.DoubleClick => "double_click",
			CloudAccessOrigin.KeyboardOpen => "keyboard_open",
			_ => "unknown"
		};

		public static string Decision(CloudPolicyDecisionKind decision) => decision switch
		{
			CloudPolicyDecisionKind.Allowed => "allowed",
			CloudPolicyDecisionKind.Observed => "observed",
			CloudPolicyDecisionKind.CachedOnly => "cached_only",
			CloudPolicyDecisionKind.Deferred => "deferred",
			CloudPolicyDecisionKind.GenericFallback => "generic_fallback",
			CloudPolicyDecisionKind.Redirected => "redirected",
			CloudPolicyDecisionKind.Blocked => "blocked",
			_ => "not_applicable"
		};

		public static string Outcome(CloudOperationOutcome outcome) => outcome switch
		{
			CloudOperationOutcome.Success => "success",
			CloudOperationOutcome.Cancelled => "cancelled",
			_ => "failure"
		};

		public static string Thumbnail(CloudThumbnailResult result) => result switch
		{
			CloudThumbnailResult.CacheHit => "cache_hit",
			CloudThumbnailResult.CacheMiss => "cache_miss",
			CloudThumbnailResult.GenericFallback => "generic_fallback",
			_ => "not_applicable"
		};

		public static string Preview(CloudPreviewResult result) => result switch
		{
			CloudPreviewResult.DirectLoad => "direct_load",
			CloudPreviewResult.Deferred => "deferred",
			CloudPreviewResult.ExplicitLoad => "explicit_load",
			CloudPreviewResult.Cancelled => "cancelled",
			_ => "not_applicable"
		};

		public static string Error(CloudErrorCategory error) => error switch
		{
			CloudErrorCategory.None => "none",
			CloudErrorCategory.Cancelled => "cancelled",
			CloudErrorCategory.NotFound => "not_found",
			CloudErrorCategory.AccessDenied => "access_denied",
			CloudErrorCategory.IO => "io",
			CloudErrorCategory.Timeout => "timeout",
			_ => "unknown"
		};

		/// <summary>
		/// Maps an exception to a coarse category. The exception message is never used.
		/// </summary>
		public static CloudErrorCategory Categorize(Exception? exception) => exception switch
		{
			null => CloudErrorCategory.None,
			OperationCanceledException => CloudErrorCategory.Cancelled,
			TimeoutException => CloudErrorCategory.Timeout,
			System.IO.FileNotFoundException or System.IO.DirectoryNotFoundException => CloudErrorCategory.NotFound,
			UnauthorizedAccessException => CloudErrorCategory.AccessDenied,
			System.IO.IOException => CloudErrorCategory.IO,
			_ => CloudErrorCategory.Unknown
		};
	}
}
