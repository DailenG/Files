// Copyright (c) Files Community
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;

namespace Files.Shared.Cloud
{
	/// <summary>
	/// Provides location classification to determine whether paths reside on cloud-backed virtual drives.
	/// </summary>
	public interface ICloudLocationClassifier
	{
		/// <summary>
		/// Classifies the given filesystem path into a <see cref="CloudLocationContext"/>.
		/// </summary>
		/// <param name="path">The file or folder path to classify.</param>
		/// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
		/// <returns>A value task containing the classified location context.</returns>
		ValueTask<CloudLocationContext> ClassifyAsync(string path, CancellationToken cancellationToken = default);
	}
}
