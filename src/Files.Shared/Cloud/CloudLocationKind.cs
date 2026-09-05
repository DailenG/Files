// Copyright (c) Files Community
// Licensed under the MIT License.

namespace Files.Shared.Cloud
{
	/// <summary>
	/// Represents the classified category of a storage location.
	/// </summary>
	public enum CloudLocationKind
	{
		/// <summary>
		/// Local physical disk (e.g. C:\).
		/// </summary>
		Local,

		/// <summary>
		/// Standard SMB or network share.
		/// </summary>
		StandardNetwork,

		/// <summary>
		/// Egnyte Desktop App virtual drive or share.
		/// </summary>
		Egnyte,

		/// <summary>
		/// Other virtual or cloud-backed storage provider.
		/// </summary>
		OtherCloudOrVirtual,

		/// <summary>
		/// Unknown or unclassified location.
		/// </summary>
		Unknown
	}
}
