// Copyright (c) Files Community
// Licensed under the MIT License.

using Windows.Win32;
using Windows.Win32.Foundation;

namespace Files.App.Services.Cloud
{
	/// <summary>
	/// Resolves a mapped drive letter to its remote UNC name without touching file content.
	/// </summary>
	internal static class MappedDriveResolver
	{
		/// <param name="driveLetter">Drive designator such as "Z:".</param>
		/// <returns>The remote name, or null when the drive is not a network mapping.</returns>
		public static string? GetRemoteName(string driveLetter)
		{
			Span<char> remoteName = stackalloc char[300];
			uint length = (uint)remoteName.Length;

			return PInvoke.WNetGetConnection(driveLetter, remoteName, ref length) == WIN32_ERROR.NO_ERROR
				? remoteName[..(int)length].TrimEnd('\0').ToString()
				: null;
		}
	}
}
