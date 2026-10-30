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

/// <summary>Base type for every event dispatched by the SDK.</summary>
/// <remarks>
/// Concrete events are immutable records. Handlers registered for a base event type also
/// receive events of its subclasses.
/// </remarks>
public abstract record CozmoEvent
{
    /// <summary>Name of the concrete event type, used in diagnostics.</summary>
    public string EventName => GetType().Name;
}
