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

namespace Cozmo.Samples.Examples;

/// <summary>Resolves the assets copied next to the example binary.</summary>
public static class ExamplePaths
{
    /// <summary>Directory containing the example assets.</summary>
    public static string AssetsDirectory => Path.Combine(AppContext.BaseDirectory, "Assets");

    /// <summary>Directory containing the Python SDK face images.</summary>
    public static string FaceImagesDirectory => Path.Combine(AssetsDirectory, "face_images");

    /// <summary>Returns an asset path relative to the assets directory.</summary>
    public static string Asset(string relativePath)
        => Path.Combine(AssetsDirectory, relativePath);

    /// <summary>Returns the path of one of the face images.</summary>
    public static string FaceImage(string fileName)
        => Path.Combine(FaceImagesDirectory, fileName);

    /// <summary>Path of the quiz questions JSON file.</summary>
    public static string QuizQuestions => Asset("quiz_questions.json");
}
