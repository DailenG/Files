// Copyright (c) Files Community
// Licensed under the MIT License.

#nullable enable

using System;
using System.Threading.Tasks;
using Files.Shared.Cloud;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Files.Shared.Tests.Cloud
{
	[TestClass]
	public class CloudLocationClassifierTests
	{
		[TestMethod]
		public async Task ClassifyAsync_EgnyteUncPath_ReturnsEgnyte()
		{
			var classifier = new CloudLocationClassifier();
			var result = await classifier.ClassifyAsync(@"\\EgnyteDrive\Shared\Projects\Plan.pdf");

			Assert.AreEqual(CloudLocationKind.Egnyte, result.Kind);
			Assert.IsTrue(result.IsCloudBacked);
			Assert.IsTrue(result.HasHydrationRisk);
			Assert.AreEqual("egnyte", result.RootIdentifier);
		}

		[TestMethod]
		public async Task ClassifyAsync_EgnyteUncRootOnly_ReturnsEgnyte()
		{
			var classifier = new CloudLocationClassifier();
			var result = await classifier.ClassifyAsync(@"\\egnytedrive");

			Assert.AreEqual(CloudLocationKind.Egnyte, result.Kind);
			Assert.IsTrue(result.IsCloudBacked);
			Assert.IsTrue(result.HasHydrationRisk);
		}

		[TestMethod]
		public async Task ClassifyAsync_LocalDrive_ReturnsLocal()
		{
			var classifier = new CloudLocationClassifier();
			var result = await classifier.ClassifyAsync(@"C:\Users\JohnDoe\Documents\file.txt");

			Assert.AreEqual(CloudLocationKind.Local, result.Kind);
			Assert.IsFalse(result.IsCloudBacked);
			Assert.IsFalse(result.HasHydrationRisk);
		}

		[TestMethod]
		public async Task ClassifyAsync_StandardUncShare_ReturnsStandardNetwork()
		{
			var classifier = new CloudLocationClassifier();
			var result = await classifier.ClassifyAsync(@"\\corp-nas01\finance\budget.xlsx");

			Assert.AreEqual(CloudLocationKind.StandardNetwork, result.Kind);
			Assert.IsFalse(result.IsCloudBacked);
			Assert.IsFalse(result.HasHydrationRisk);
		}

		[TestMethod]
		public async Task ClassifyAsync_MappedEgnyteDrive_ResolvedViaCallback_ReturnsEgnyte()
		{
			string? Resolver(string drive) => drive.Equals("Z:", StringComparison.OrdinalIgnoreCase)
				? @"\\EgnyteDrive\Private"
				: null;

			var classifier = new CloudLocationClassifier(remotePathResolver: Resolver);
			var result = await classifier.ClassifyAsync(@"Z:\Projects\Drawing.dwg");

			Assert.AreEqual(CloudLocationKind.Egnyte, result.Kind);
			Assert.IsTrue(result.IsCloudBacked);
			Assert.IsTrue(result.HasHydrationRisk);
		}

		[TestMethod]
		public async Task ClassifyAsync_MappedStandardDrive_ResolvedViaCallback_ReturnsStandardNetwork()
		{
			string? Resolver(string drive) => drive.Equals("N:", StringComparison.OrdinalIgnoreCase)
				? @"\\fileserver\share"
				: null;

			var classifier = new CloudLocationClassifier(remotePathResolver: Resolver);
			var result = await classifier.ClassifyAsync(@"N:\Company\Policy.docx");

			Assert.AreEqual(CloudLocationKind.StandardNetwork, result.Kind);
			Assert.IsFalse(result.IsCloudBacked);
		}

		[TestMethod]
		public async Task ClassifyAsync_ConfiguredRootOverride_ReturnsEgnyte()
		{
			var classifier = new CloudLocationClassifier(configuredEgnyteRoots: new[] { "E:\\" });
			var result = await classifier.ClassifyAsync(@"E:\Department\Data.csv");

			Assert.AreEqual(CloudLocationKind.Egnyte, result.Kind);
			Assert.IsTrue(result.IsCloudBacked);
			Assert.IsTrue(result.HasHydrationRisk);
		}

		[TestMethod]
		public async Task ClassifyAsync_EmptyOrNull_ReturnsUnknown()
		{
			var classifier = new CloudLocationClassifier();
			var result = await classifier.ClassifyAsync(string.Empty);

			Assert.AreEqual(CloudLocationKind.Unknown, result.Kind);
		}
	}
}
