// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.PlatformAbstractions;
using Microsoft.NET.Build.Tasks;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;
using System;
using System.IO;

namespace Microsoft.NET.Publish.Tests
{
    public class GivenThatWeWantToPublishASingleFileApp : SdkTest
    {
        private const string TestProjectName = "HelloWorldWithSubDirs";

        private const string PublishSingleFile = "/p:PublishSingleFile=true";
        private const string FrameworkDependent = "/p:SelfContained=false";
        private const string IncludePdb = "/p:IncludeSymbolsInSingleFile=true";
        private const string ExcludeContent = "/p:ExcludeContent=true";
        private const string DontUseAppHost = "/p:UseAppHost=false";
        private const string ReadyToRun = "/p:PublishReadyToRun=true";
        private const string ReadyToRunWithSymbols = "/p:PublishReadyToRunEmitSymbols=true";

        private readonly string RuntimeIdentifier = $"/p:RuntimeIdentifier={RuntimeEnvironment.GetRuntimeIdentifier()}";
        private readonly string SingleFile = $"{TestProjectName}{Constants.ExeSuffix}";
        private readonly string PdbFile = $"{TestProjectName}.pdb";
        private readonly string NiPdbFile = $"{TestProjectName}.ni.pdb";
        private const string ContentFile = "Signature.stamp";

        public GivenThatWeWantToPublishASingleFileApp(ITestOutputHelper log) : base(log)
        {
        }

        private PublishCommand GetPublishCommand()
        {
            var testAsset = _testAssetsManager
               .CopyTestAsset(TestProjectName)
               .WithSource();

            return new PublishCommand(Log, testAsset.TestRoot);
        }

        private DirectoryInfo GetPublishDirectory(PublishCommand publishCommand)
        {
            return publishCommand.GetOutputDirectory(targetFramework: "netcoreapp3.0",
                                                     runtimeIdentifier: RuntimeEnvironment.GetRuntimeIdentifier());
        }

        [Fact]
        public void It_errors_when_publishing_single_file_app_without_rid()
        {
            GetPublishCommand()
                .Execute(PublishSingleFile)
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining(Strings.CannotHaveSingleFileWithoutRuntimeIdentifier);
        }

        [Fact]
        public void It_errors_when_publishing_single_file_without_apphost()
        {
            GetPublishCommand()
                .Execute(PublishSingleFile, RuntimeIdentifier, FrameworkDependent, DontUseAppHost)
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining(Strings.CannotHaveSingleFileWithoutAppHost);
        }

        [Fact]
        public void It_errors_when_publishing_single_file_lib()
        {
            var testProject = new TestProject()
            {
                Name = "SingleFileClassLib",
                TargetFrameworks = "netstandard2.0",
                IsSdkProject = true,
                IsExe = false,
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var publishCommand = new PublishCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));

            publishCommand.Execute(PublishSingleFile, RuntimeIdentifier)
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining(Strings.CannotHaveSingleFileWithoutExecutable);
        }

        [Fact]
        public void It_generates_a_single_file_for_framework_dependent_apps()
        {
            var publishCommand = GetPublishCommand();
            publishCommand
                .Execute(PublishSingleFile, RuntimeIdentifier, FrameworkDependent)
                .Should()
                .Pass();

            string[] expectedFiles = { SingleFile, PdbFile };
            GetPublishDirectory(publishCommand)
                .Should()
                .OnlyHaveFiles(expectedFiles);
        }

        [Fact]
        public void It_generates_a_single_file_for_self_contained_apps()
        {
            var publishCommand = GetPublishCommand();
            publishCommand
                .Execute(PublishSingleFile, RuntimeIdentifier)
                .Should()
                .Pass();

            string[] expectedFiles = { SingleFile, PdbFile };
            GetPublishDirectory(publishCommand)
                .Should()
                .OnlyHaveFiles(expectedFiles);
        }

        [Fact]
        public void It_generates_a_single_file_including_pdbs()
        {
            var publishCommand = GetPublishCommand();
            publishCommand
                .Execute(PublishSingleFile, RuntimeIdentifier, IncludePdb)
                .Should()
                .Pass();

            string[] expectedFiles = { SingleFile };
            GetPublishDirectory(publishCommand)
                .Should()
                .OnlyHaveFiles(expectedFiles);
        }

        [WindowsOnlyFact]
        public void It_excludes_ni_pdbs_from_single_file()
        {
            var publishCommand = GetPublishCommand();
            publishCommand
                .Execute(PublishSingleFile, RuntimeIdentifier, ReadyToRun, ReadyToRunWithSymbols)
                .Should()
                .Pass();

            string[] expectedFiles = { SingleFile, PdbFile, NiPdbFile };
            GetPublishDirectory(publishCommand)
                .Should()
                //  TODO: Change HaveFiles to OnlyHaveFiles, once https://github.com/dotnet/coreclr/issues/25522 is fixed
                .HaveFiles(expectedFiles);
        }

        [WindowsOnlyFact]
        public void It_can_include_ni_pdbs_in_single_file()
        {
            var publishCommand = GetPublishCommand();
            publishCommand
                .Execute(PublishSingleFile, RuntimeIdentifier, ReadyToRun, ReadyToRunWithSymbols, IncludePdb)
                .Should()
                .Pass();

            string[] expectedFiles = { SingleFile };
            GetPublishDirectory(publishCommand)
                .Should()
                .OnlyHaveFiles(expectedFiles);
        }

        [Fact]
        public void It_generates_a_single_file_excluding_content()
        {
            var publishCommand = GetPublishCommand();
            publishCommand
                .Execute(PublishSingleFile, RuntimeIdentifier, ExcludeContent)
                .Should()
                .Pass();

            string[] expectedFiles = { SingleFile, PdbFile, ContentFile };
            GetPublishDirectory(publishCommand)
                .Should()
                .OnlyHaveFiles(expectedFiles);
        }

        [Fact]
        public void It_generates_a_single_file_for_R2R_compiled_Apps()
        {
            var publishCommand = GetPublishCommand();
            publishCommand
                .Execute(PublishSingleFile, RuntimeIdentifier, ReadyToRun)
                .Should()
                .Pass();

            string[] expectedFiles = { SingleFile, PdbFile };
            GetPublishDirectory(publishCommand)
                .Should()
                .OnlyHaveFiles(expectedFiles);
        }

        [Fact]
        public void It_does_not_rewrite_the_single_file_unnecessarily()
        {
            var publishCommand = GetPublishCommand();
            var singleFilePath = Path.Combine(GetPublishDirectory(publishCommand).FullName, SingleFile);

            publishCommand
                .Execute(PublishSingleFile, RuntimeIdentifier, FrameworkDependent)
                .Should()
                .Pass();
            DateTime fileWriteTimeAfterFirstRun = File.GetLastWriteTimeUtc(singleFilePath);

            WaitForUtcNowToAdvance();

            publishCommand
                .Execute(PublishSingleFile, RuntimeIdentifier, FrameworkDependent)
                .Should()
                .Pass();
            DateTime fileWriteTimeAfterSecondRun = File.GetLastWriteTimeUtc(singleFilePath);

            fileWriteTimeAfterSecondRun.Should().Be(fileWriteTimeAfterFirstRun);
        }

        [Fact]
        public void It_rewrites_the_apphost_for_single_file_publish()
        {
            var publishCommand = GetPublishCommand();
            var appHostPath = Path.Combine(GetPublishDirectory(publishCommand).FullName, SingleFile);
            var singleFilePath = appHostPath;

            publishCommand
                .Execute(RuntimeIdentifier, FrameworkDependent)
                .Should()
                .Pass();
            var appHostSize = new FileInfo(appHostPath).Length;

            WaitForUtcNowToAdvance();

            publishCommand
                .Execute(PublishSingleFile, RuntimeIdentifier, FrameworkDependent)
                .Should()
                .Pass();
            var singleFileSize = new FileInfo(singleFilePath).Length;

            singleFileSize.Should().BeGreaterThan(appHostSize);
        }

        [Fact]
        public void It_rewrites_the_apphost_for_non_single_file_publish()
        {
            var publishCommand = GetPublishCommand();
            var appHostPath = Path.Combine(GetPublishDirectory(publishCommand).FullName, SingleFile);
            var singleFilePath = appHostPath;

            publishCommand
                .Execute(PublishSingleFile, RuntimeIdentifier, FrameworkDependent)
                .Should()
                .Pass();
            var singleFileSize = new FileInfo(singleFilePath).Length;

            WaitForUtcNowToAdvance();

            publishCommand
                .Execute(RuntimeIdentifier, FrameworkDependent)
                .Should()
                .Pass();
            var appHostSize = new FileInfo(appHostPath).Length;

            appHostSize.Should().BeLessThan(singleFileSize);
        }
    }
}
