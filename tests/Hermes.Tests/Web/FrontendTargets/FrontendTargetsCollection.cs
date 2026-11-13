// Copyright (c) Mythetech. Licensed under the MIT License.
using Xunit;

namespace Hermes.Tests.Web.FrontendTargets;

/// <summary>One child build at a time per test run, so the builds do not load timing-sensitive tests.</summary>
[CollectionDefinition(nameof(FrontendTargetsCollection))]
public sealed class FrontendTargetsCollection;
