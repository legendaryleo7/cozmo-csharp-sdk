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

namespace Anki.Cozmo.Sdk;

/// <summary>Marker interface for the world that owns observable elements.</summary>
/// <remarks>
/// <para>
/// Python's <c>ObservableElement</c> stores a reference to <c>cozmo.world.World</c>
/// (objects.py:163). The C# objects layer is compiled before the world layer (WP-11), so it
/// cannot name the concrete <c>World</c> type without introducing a dependency cycle. This
/// empty interface lets an element keep that back-reference while WP-11's <c>World</c>
/// implements the interface and remains the only type that knows how objects are routed.
/// </para>
/// <para>
/// The interface deliberately has no members: the objects layer never calls into the world.
/// </para>
/// </remarks>
public interface IWorldContext
{
}
