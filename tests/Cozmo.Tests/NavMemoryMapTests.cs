// Copyright (c) 2016-2017 Anki, Inc.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License in the file LICENSE.txt or at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Anki.Cozmo.ExternalInterface;
using Anki.Cozmo.Sdk;

namespace Cozmo.Tests;

/// <summary>One node in a Python-generated pre-order dump of a quad-tree.</summary>
internal sealed record NavNodeDump(
    int Depth,
    double Size,
    double CenterX,
    double CenterY,
    double CenterZ,
    int ChildCount,
    string? ContentName);

/// <summary>A Python-generated point query against a quad-tree.</summary>
internal sealed record NavQuery(
    double X,
    double Y,
    bool HasNode,
    int NodeDepth,
    double NodeCenterX,
    double NodeCenterY,
    double NodeCenterZ,
    string? ContentName);

/// <summary>Tests the WP-10 port of <c>cozmo.nav_memory_map</c>.</summary>
public sealed class NavMemoryMapTests
{
    [Fact]
    public void ContentTypeIdsMatchThePythonSdk()
    {
        foreach ((string name, long id) in NavGoldenData.ContentTypes)
        {
            CladEnum<ENodeContentTypeEnum>? content = NodeContentTypes.FindById(id);

            CladEnum<ENodeContentTypeEnum> entry = Assert.IsType<CladEnum<ENodeContentTypeEnum>>(content);
            Assert.Equal(name, entry.Name);
            Assert.Equal(id, entry.Id);
            Assert.Equal(id, Convert.ToInt64(entry.Value));
        }

        Assert.Null(NodeContentTypes.FindById(99));
    }

    [Fact]
    public void NamedContentTypesExposeTheirCladValues()
    {
        Assert.Equal((long)ENodeContentTypeEnum.Unknown, NodeContentTypes.Unknown.Id);
        Assert.Equal((long)ENodeContentTypeEnum.ClearOfObstacle, NodeContentTypes.ClearOfObstacle.Id);
        Assert.Equal((long)ENodeContentTypeEnum.ClearOfCliff, NodeContentTypes.ClearOfCliff.Id);
        Assert.Equal((long)ENodeContentTypeEnum.ObstacleCube, NodeContentTypes.ObstacleCube.Id);
        Assert.Equal((long)ENodeContentTypeEnum.ObstacleCharger, NodeContentTypes.ObstacleCharger.Id);
        Assert.Equal((long)ENodeContentTypeEnum.Cliff, NodeContentTypes.Cliff.Id);
        Assert.Equal((long)ENodeContentTypeEnum.VisionBorder, NodeContentTypes.VisionBorder.Id);
        Assert.Equal((long)ENodeContentTypeEnum.ObstacleProx, NodeContentTypes.ObstacleProx.Id);
    }

    [Fact]
    public void SizeCenterAndOriginMatchTheConstructor()
    {
        NavMemoryMapGrid grid = CreateGoldenGrid();

        Assert.Equal(NavGoldenData.OriginId, grid.OriginId);
        Assert.Equal(NavGoldenData.RootDepth, grid.RootNode.Depth);
        Assert.Equal(NavGoldenData.RootSize, grid.Size);
        Assert.Equal(NavGoldenData.RootCenterX, grid.Center.X);
        Assert.Equal(NavGoldenData.RootCenterY, grid.Center.Y);
        Assert.Equal(0.0, grid.Center.Z);
        Assert.Null(grid.RootNode.Parent);
    }

    [Fact]
    public void ContainsPointMatchesPython()
    {
        NavMemoryMapGrid grid = CreateGoldenGrid();

        foreach ((double x, double y, bool expected) in NavGoldenData.ContainsCases)
        {
            Assert.Equal(expected, grid.ContainsPoint(x, y));
            Assert.Equal(expected, grid.RootNode.ContainsPoint(x, y));
        }
    }

    [Fact]
    public void QuadTreeStructureMatchesPython()
    {
        NavMemoryMapGrid grid = CreateGoldenGrid();
        List<NavMemoryMapGridNode> nodes = Preorder(grid.RootNode).ToList();

        Assert.Equal(NavGoldenData.Nodes.Length, nodes.Count);
        for (int i = 0; i < nodes.Count; i++)
        {
            NavMemoryMapGridNode node = nodes[i];
            NavNodeDump expected = NavGoldenData.Nodes[i];
            Assert.Equal(expected.Depth, node.Depth);
            Assert.Equal(expected.Size, node.Size);
            Assert.Equal(expected.CenterX, node.Center.X);
            Assert.Equal(expected.CenterY, node.Center.Y);
            Assert.Equal(expected.CenterZ, node.Center.Z);
            Assert.Equal(expected.ChildCount, node.Children?.Count ?? 0);
            Assert.Equal(expected.ContentName, node.Content?.Name);
        }

        Assert.Null(grid.RootNode.Parent);
        foreach (NavMemoryMapGridNode node in nodes)
        {
            if (node.Children is null)
            {
                continue;
            }

            foreach (NavMemoryMapGridNode child in node.Children)
            {
                Assert.Same(node, child.Parent);
            }
        }
    }

    [Fact]
    public void GetNodeAndGetContentMatchPython()
    {
        NavMemoryMapGrid grid = CreateGoldenGrid();

        foreach (NavQuery query in NavGoldenData.Queries)
        {
            NavMemoryMapGridNode? node = grid.GetNode(query.X, query.Y);
            Assert.Equal(query.HasNode, node is not null);
            Assert.Equal(query.HasNode, grid.ContainsPoint(query.X, query.Y));

            if (node is not null)
            {
                Assert.Equal(query.NodeDepth, node.Depth);
                Assert.Equal(query.NodeCenterX, node.Center.X);
                Assert.Equal(query.NodeCenterY, node.Center.Y);
                Assert.Equal(query.NodeCenterZ, node.Center.Z);
            }

            CladEnum<ENodeContentTypeEnum>? content = grid.GetContent(query.X, query.Y);
            Assert.Equal(query.ContentName, content?.Name);
        }
    }

    [Fact]
    public void GetContentReturnsUnknownOutsideAndNullForUnfilledNodes()
    {
        NavMemoryMapGrid grid = CreateGoldenGrid();

        Assert.Equal(NodeContentTypes.Unknown, grid.GetContent(1000.0, 1000.0));
        Assert.Null(grid.GetContent(-27.5, 17.5));
        Assert.Equal(NodeContentTypes.VisionBorder, grid.GetContent(8.4375, 15.9375));
    }

    [Fact]
    public void ASingleQuadCanFillTheRoot()
    {
        var grid = new NavMemoryMapGrid(1, 2, 8.0, 10.0, 20.0);

        grid.AddQuad(NodeContentTypes.Cliff, 2);

        Assert.Equal(NodeContentTypes.Cliff, grid.RootNode.Content);
        Assert.Null(grid.RootNode.Children);
        Assert.Same(grid.RootNode, grid.GetNode(10.0, 20.0));
        Assert.Equal(NodeContentTypes.Cliff, grid.GetContent(10.0, 20.0));
        Assert.Equal(NodeContentTypes.Unknown, grid.GetContent(100.0, 100.0));
    }

    [Fact]
    public void TheCladEnumOverloadAcceptsANamedContentType()
    {
        var grid = new NavMemoryMapGrid(1, 0, 4.0, 0.0, 0.0);

        grid.AddQuad(ENodeContentTypeEnum.ClearOfCliff, 0);

        Assert.Equal(NodeContentTypes.ClearOfCliff, grid.GetContent(0.0, 0.0));
    }

    [Fact]
    public void MalformedDepthIsRejectedBeforeTheTreeChanges()
    {
        var grid = new NavMemoryMapGrid(1, 5, 100.0, 0.0, 0.0);

        Assert.Throws<ArgumentOutOfRangeException>(() => grid.AddQuad(NodeContentTypes.Cliff, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => grid.AddQuad(NodeContentTypes.Cliff, 6));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => grid.AddQuad((ENodeContentTypeEnum)200, 2));
        Assert.Null(grid.RootNode.Content);
        Assert.Null(grid.RootNode.Children);
    }

    [Fact]
    public void FillingEveryChildSlotThenAddingAnotherQuadThrows()
    {
        var grid = new NavMemoryMapGrid(1, 2, 10.0, 0.0, 0.0);

        grid.AddQuad(NodeContentTypes.ClearOfObstacle, 1);
        grid.AddQuad(NodeContentTypes.ObstacleCube, 1);
        grid.AddQuad(NodeContentTypes.Cliff, 1);
        grid.AddQuad(NodeContentTypes.ClearOfCliff, 1);

        Assert.Throws<InvalidOperationException>(
            () => grid.AddQuad(NodeContentTypes.VisionBorder, 1));
    }

    private static NavMemoryMapGrid CreateGoldenGrid()
    {
        var grid = new NavMemoryMapGrid(
            NavGoldenData.OriginId,
            NavGoldenData.RootDepth,
            NavGoldenData.RootSize,
            NavGoldenData.RootCenterX,
            NavGoldenData.RootCenterY);

        foreach ((byte contentId, int depth) in NavGoldenData.Adds)
        {
            CladEnum<ENodeContentTypeEnum>? content = NodeContentTypes.FindById(contentId);
            Assert.NotNull(content);
            grid.AddQuad(content, depth);
        }

        return grid;
    }

    private static IEnumerable<NavMemoryMapGridNode> Preorder(NavMemoryMapGridNode node)
    {
        yield return node;
        if (node.Children is null)
        {
            yield break;
        }

        foreach (NavMemoryMapGridNode child in node.Children)
        {
            foreach (NavMemoryMapGridNode descendant in Preorder(child))
            {
                yield return descendant;
            }
        }
    }
}
