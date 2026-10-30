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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Anki.Cozmo.Sdk;

/// <summary>Raised when a new navigation memory map is received.</summary>
/// <param name="NavMemoryMap">The new navigation memory map.</param>
public sealed record EvtNewNavMemoryMap(NavMemoryMapGrid NavMemoryMap) : CozmoEvent;

/// <summary>The content types for a <see cref="NavMemoryMapGridNode"/>.</summary>
/// <remarks>
/// Each member wraps a value of the CLAD <c>ENodeContentTypeEnum</c> and exposes its name and
/// numeric id.
/// </remarks>
public static class NodeContentTypes
{
    /// <summary>The contents of the node are unknown.</summary>
    public static CladEnum<ENodeContentTypeEnum> Unknown { get; } =
        CladEnum<ENodeContentTypeEnum>.FromValue(ENodeContentTypeEnum.Unknown);

    /// <summary>
    /// The node is clear of obstacles because Cozmo has seen objects on the other side, but
    /// it might contain a cliff. The node becomes <see cref="Cliff"/> or
    /// <see cref="ClearOfCliff"/> once Cozmo has driven there.
    /// </summary>
    public static CladEnum<ENodeContentTypeEnum> ClearOfObstacle { get; } =
        CladEnum<ENodeContentTypeEnum>.FromValue(ENodeContentTypeEnum.ClearOfObstacle);

    /// <summary>The node is clear of any cliffs (a sharp drop) or obstacles.</summary>
    public static CladEnum<ENodeContentTypeEnum> ClearOfCliff { get; } =
        CladEnum<ENodeContentTypeEnum>.FromValue(ENodeContentTypeEnum.ClearOfCliff);

    /// <summary>The node contains a light cube.</summary>
    public static CladEnum<ENodeContentTypeEnum> ObstacleCube { get; } =
        CladEnum<ENodeContentTypeEnum>.FromValue(ENodeContentTypeEnum.ObstacleCube);

    /// <summary>The node contains a charger.</summary>
    public static CladEnum<ENodeContentTypeEnum> ObstacleCharger { get; } =
        CladEnum<ENodeContentTypeEnum>.FromValue(ENodeContentTypeEnum.ObstacleCharger);

    /// <summary>The node contains a cliff (a sharp drop).</summary>
    public static CladEnum<ENodeContentTypeEnum> Cliff { get; } =
        CladEnum<ENodeContentTypeEnum>.FromValue(ENodeContentTypeEnum.Cliff);

    /// <summary>The node contains a visible edge, based on the camera feed.</summary>
    public static CladEnum<ENodeContentTypeEnum> VisionBorder { get; } =
        CladEnum<ENodeContentTypeEnum>.FromValue(ENodeContentTypeEnum.VisionBorder);

    /// <summary>
    /// The node is near an obstacle, as reported by the prox sensors.
    /// </summary>
    public static CladEnum<ENodeContentTypeEnum> ObstacleProx { get; } =
        CladEnum<ENodeContentTypeEnum>.FromValue(ENodeContentTypeEnum.ObstacleProx);

    /// <summary>Looks up a content type by its CLAD numeric id.</summary>
    /// <param name="id">Numeric id of the content type.</param>
    /// <returns>The matching content type, or <see langword="null"/> when the id is unknown.</returns>
    public static CladEnum<ENodeContentTypeEnum>? FindById(long id) =>
        CladEnum<ENodeContentTypeEnum>.TryFromId(id, out CladEnum<ENodeContentTypeEnum>? entry)
            ? entry
            : null;

    internal static CladEnum<ENodeContentTypeEnum> FromValue(ENodeContentTypeEnum value) =>
        CladEnum<ENodeContentTypeEnum>.FromValue(value);
}

/// <summary>A node in a <see cref="NavMemoryMapGrid"/>.</summary>
/// <remarks>
/// <para>
/// Leaf nodes contain content; all other nodes are split into four equally sized children.
/// Child indices use the following x/y orientation:
/// </para>
/// <code>
/// +---+----+---+
/// | ^ | 2  | 0 |
/// +---+----+---+
/// | Y | 3  | 1 |
/// +---+----+---+
/// |   | X-&gt;|   |
/// +---+----+---+
/// </code>
/// </remarks>
public sealed class NavMemoryMapGridNode
{
    private readonly ILogger _logger;
    private List<NavMemoryMapGridNode>? _children;
    private int _nextChild;

    /// <summary>Creates a node.</summary>
    /// <param name="depth">Depth of this node in the quad-tree.</param>
    /// <param name="size">Width and length of this square node.</param>
    /// <param name="center">Center of this node.</param>
    /// <param name="parent">Parent node, or <see langword="null"/> for the root node.</param>
    /// <param name="logger">Optional logger; defaults to a no-op logger.</param>
    /// <exception cref="ArgumentNullException"><paramref name="center"/> is <see langword="null"/>.</exception>
    public NavMemoryMapGridNode(
        int depth,
        double size,
        Vector3 center,
        NavMemoryMapGridNode? parent,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(center);
        Depth = depth;
        Size = size;
        Center = center;
        Parent = parent;
        _logger = logger ?? NullLogger.Instance;
    }

    /// <summary>Depth of this node; how far down the quad-tree it is.</summary>
    public int Depth { get; }

    /// <summary>Width and length of this square node.</summary>
    public double Size { get; }

    /// <summary>The center of this node.</summary>
    public Vector3 Center { get; }

    /// <summary>The parent of this node, or <see langword="null"/> for the root node.</summary>
    public NavMemoryMapGridNode? Parent { get; }

    /// <summary>
    /// The four child nodes, or <see langword="null"/> for a leaf node.
    /// </summary>
    public IReadOnlyList<NavMemoryMapGridNode>? Children => _children;

    /// <summary>
    /// The content type in this node. Only leaf nodes normally have content, and nodes that
    /// were never filled in the serialized map have <see langword="null"/> content.
    /// </summary>
    public CladEnum<ENodeContentTypeEnum>? Content { get; internal set; }

    /// <summary>Tests whether this node contains the given x/y coordinates.</summary>
    /// <param name="x">x coordinate of the point.</param>
    /// <param name="y">y coordinate of the point.</param>
    /// <returns>True when the point is on or inside this node; false otherwise.</returns>
    public bool ContainsPoint(double x, double y)
    {
        double halfSize = Size * 0.5;
        double distanceX = Math.Abs(Center.X - x);
        double distanceY = Math.Abs(Center.Y - y);
        return distanceX <= halfSize && distanceY <= halfSize;
    }

    /// <summary>Gets the smallest node that includes the given x/y coordinates.</summary>
    /// <param name="x">x coordinate of the point.</param>
    /// <param name="y">y coordinate of the point.</param>
    /// <returns>
    /// The smallest node that includes the point, or <see langword="null"/> when the point is
    /// outside the map.
    /// </returns>
    public NavMemoryMapGridNode? GetNode(double x, double y) => GetNode(x, y, assumedInBounds: false);

    /// <summary>Gets the content at the given x/y coordinates.</summary>
    /// <param name="x">x coordinate of the point.</param>
    /// <param name="y">y coordinate of the point.</param>
    /// <returns>
    /// The content included at that point. Outside the map this is
    /// <see cref="NodeContentTypes.Unknown"/>; a point inside a node that was never filled in
    /// has <see langword="null"/> content.
    /// </returns>
    public CladEnum<ENodeContentTypeEnum>? GetContent(double x, double y)
    {
        NavMemoryMapGridNode? node = GetNode(x, y);
        return node is not null ? node.Content : NodeContentTypes.Unknown;
    }

    /// <inheritdoc />
    public override string ToString() =>
        FormattableString.Invariant(
            $"<{GetType().Name} center: {Center} size: {Size} content: {Content?.Name ?? "None"}>");

    /// <summary>Adds one leaf of the serialized quad-tree.</summary>
    /// <param name="content">Content for the leaf node.</param>
    /// <param name="depth">Depth at which the leaf is located.</param>
    /// <returns>
    /// True when the caller should move on to its next child for future additions; false
    /// when this node still has free child slots along the current branch.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="content"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">All four child slots of this node are full.</exception>
    internal bool AddChild(CladEnum<ENodeContentTypeEnum> content, int depth)
    {
        ArgumentNullException.ThrowIfNull(content);

        // nav_memory_map.py:213-216 reports these consistency violations but keeps going.
        if (depth > Depth)
        {
            _logger.LogError(
                "NavMemoryMapGridNode depth {RequestedDepth} > {NodeDepth}.",
                depth,
                Depth);
        }

        if (_nextChild > 3)
        {
            _logger.LogError(
                "NavMemoryMapGridNode _next_child {NextChild} (>3) at depth {NodeDepth}.",
                _nextChild,
                Depth);
        }

        if (Depth == depth)
        {
            if (Content is not null)
            {
                _logger.LogError(
                    "NavMemoryMapGridNode: Clobbering {ExistingContent} at depth {NodeDepth} with {NewContent}.",
                    Content,
                    Depth,
                    content);
            }

            Content = content;
            // This node will not be further subdivided and is now full.
            return true;
        }

        if (_children is null)
        {
            // Create four child nodes for the quad-tree structure
            // (nav_memory_map.py:226-238). The tree serializes leaves in the order
            // (+x,+y), (+x,-y), (-x,+y), (-x,-y).
            int nextDepth = Depth - 1;
            double nextSize = Size * 0.5;
            double offset = nextSize * 0.5;
            _children = new List<NavMemoryMapGridNode>(4)
            {
                new(nextDepth, nextSize, new Vector3(Center.X + offset, Center.Y + offset, Center.Z), this, _logger),
                new(nextDepth, nextSize, new Vector3(Center.X + offset, Center.Y - offset, Center.Z), this, _logger),
                new(nextDepth, nextSize, new Vector3(Center.X - offset, Center.Y + offset, Center.Z), this, _logger),
                new(nextDepth, nextSize, new Vector3(Center.X - offset, Center.Y - offset, Center.Z), this, _logger),
            };
        }

        if (_nextChild > 3)
        {
            // Python logs and then raises IndexError when it indexes children[4].
            throw new InvalidOperationException(
                FormattableString.Invariant(
                    $"All four child slots at depth {Depth} are already full."));
        }

        if (_children[_nextChild].AddChild(content, depth))
        {
            // Child node is now full; start using the next child.
            _nextChild++;
        }

        // When all children are full the parent should start using its next child.
        return _nextChild > 3;
    }

    private NavMemoryMapGridNode? GetNode(double x, double y, bool assumedInBounds)
    {
        if (!assumedInBounds && !ContainsPoint(x, y))
        {
            return null;
        }

        if (_children is null)
        {
            return this;
        }

        int xOffset = x < Center.X ? 2 : 0;
        int yOffset = y < Center.Y ? 1 : 0;
        return _children[xOffset + yOffset].GetNode(x, y, assumedInBounds: true);
    }
}

/// <summary>A navigation memory map, stored as a quad-tree.</summary>
/// <remarks>
/// <para>
/// Cozmo builds this map of the navigable world from objects, cliffs and visible edges. Unlike
/// an occupancy map, each node stores a content type rather than a probability of occupancy.
/// </para>
/// <para>
/// To receive maps the world layer must request them from the robot; the map itself is
/// received as <see cref="EvtNewNavMemoryMap"/>.
/// </para>
/// </remarks>
public sealed class NavMemoryMapGrid
{
    private readonly NavMemoryMapGridNode _rootNode;

    /// <summary>Creates an empty navigation memory map.</summary>
    /// <param name="originId">
    /// Origin id for the map. Only maps and poses with the same origin id share a coordinate
    /// frame and can be compared.
    /// </param>
    /// <param name="rootDepth">Depth of the root node.</param>
    /// <param name="rootSize">Width and length of the square grid.</param>
    /// <param name="rootCenterX">x coordinate of the center of the grid.</param>
    /// <param name="rootCenterY">y coordinate of the center of the grid.</param>
    /// <param name="logger">Optional logger; defaults to a no-op logger.</param>
    public NavMemoryMapGrid(
        int originId,
        int rootDepth,
        double rootSize,
        double rootCenterX,
        double rootCenterY,
        ILogger<NavMemoryMapGrid>? logger = null)
    {
        OriginId = originId;
        _rootNode = new NavMemoryMapGridNode(
            rootDepth,
            rootSize,
            new Vector3(rootCenterX, rootCenterY, 0.0),
            parent: null,
            logger);
    }

    /// <summary>Origin id for the map.</summary>
    /// <remarks>
    /// Only maps and poses with the same origin id share a coordinate frame and can be
    /// compared.
    /// </remarks>
    public int OriginId { get; }

    /// <summary>The root node for the grid, which contains all other nodes.</summary>
    public NavMemoryMapGridNode RootNode => _rootNode;

    /// <summary>Width and length of the square grid.</summary>
    public double Size => _rootNode.Size;

    /// <summary>The center of this map.</summary>
    public Vector3 Center => _rootNode.Center;

    /// <summary>Tests whether the map contains the given x/y coordinates.</summary>
    /// <param name="x">x coordinate of the point.</param>
    /// <param name="y">y coordinate of the point.</param>
    /// <returns>True when the map contains the point; false otherwise.</returns>
    public bool ContainsPoint(double x, double y) => _rootNode.ContainsPoint(x, y);

    /// <summary>Gets the smallest node that includes the given x/y coordinates.</summary>
    /// <param name="x">x coordinate of the point.</param>
    /// <param name="y">y coordinate of the point.</param>
    /// <returns>
    /// The smallest node that includes the point, or <see langword="null"/> when the point is
    /// outside the map.
    /// </returns>
    public NavMemoryMapGridNode? GetNode(double x, double y) => _rootNode.GetNode(x, y);

    /// <summary>Gets the content at the given x/y coordinates.</summary>
    /// <param name="x">x coordinate of the point.</param>
    /// <param name="y">y coordinate of the point.</param>
    /// <returns>
    /// The content included at that point. Outside the map this is
    /// <see cref="NodeContentTypes.Unknown"/>; a point inside a node that was never filled in
    /// has <see langword="null"/> content.
    /// </returns>
    public CladEnum<ENodeContentTypeEnum>? GetContent(double x, double y) => _rootNode.GetContent(x, y);

    /// <summary>Adds one serialized quad to the quad-tree.</summary>
    /// <param name="content">Content type stored in the leaf node.</param>
    /// <param name="depth">Depth at which the leaf is located.</param>
    /// <exception cref="ArgumentNullException"><paramref name="content"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="depth"/> is negative or deeper than <see cref="RootNode"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">The tree has no free leaf slot where the quad belongs.</exception>
    /// <remarks>
    /// Rejects a depth beyond the root depth before modifying the tree.
    /// </remarks>
    public void AddQuad(CladEnum<ENodeContentTypeEnum> content, int depth)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (depth < 0 || depth > _rootNode.Depth)
        {
            throw new ArgumentOutOfRangeException(
                nameof(depth),
                depth,
                FormattableString.Invariant(
                    $"depth must be between 0 and the root depth ({_rootNode.Depth})."));
        }

        _rootNode.AddChild(content, depth);
    }

    /// <summary>Adds one serialized quad to the quad-tree.</summary>
    /// <param name="content">Content type stored in the leaf node.</param>
    /// <param name="depth">Depth at which the leaf is located.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="content"/> is not a named <see cref="ENodeContentTypeEnum"/> value, or
    /// <paramref name="depth"/> is out of range.
    /// </exception>
    public void AddQuad(ENodeContentTypeEnum content, int depth)
    {
        if (!Enum.IsDefined(content))
        {
            throw new ArgumentOutOfRangeException(
                nameof(content),
                content,
                "content is not a named ENodeContentTypeEnum value.");
        }

        AddQuad(NodeContentTypes.FromValue(content), depth);
    }
}
