using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace UnderstudyKingdom.Tests
{
    /// <summary>
    /// Regression guard recommended by two separate final whole-branch reviews
    /// (milestones #12 and #13) but never built until now: a future AI-art
    /// re-export that ships one mismatched image dimension in an otherwise
    /// uniform set previously slipped through review entirely (milestone #12's
    /// 3-of-15 mismatched ruler portraits, caught only by a human manual
    /// checkpoint) and visibly distorted under `preserveAspect`. This asserts
    /// every PNG already committed in each themed-art folder shares one
    /// dimension -- folder-count-agnostic, so it keeps working as more icons
    /// land in ButtonIcons without needing updates here. See
    /// docs/PROJECT_PLAN.md's milestone #12 follow-up note.
    /// </summary>
    public class ArtAssetDimensionTests
    {
        private static IEnumerable<(int width, int height)> LoadDimensions(string folder)
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Assert.IsNotNull(texture, $"Failed to load texture at {path}");
                yield return (texture.width, texture.height);
            }
        }

        private static void AssertAllSameDimensions(string folder)
        {
            var dimensions = new List<(int width, int height)>(LoadDimensions(folder));
            Assert.IsNotEmpty(dimensions, $"No textures found in {folder} -- folder path may have moved.");

            (int width, int height) expected = dimensions[0];
            foreach ((int width, int height) actual in dimensions)
            {
                Assert.AreEqual(expected, actual,
                    $"{folder} has a mismatched image dimension ({actual.width}x{actual.height} vs the rest at {expected.width}x{expected.height}) -- " +
                    "a re-exported/regenerated image in this set doesn't match the others, which will visibly resize/distort under preserveAspect.");
            }
        }

        [Test]
        public void RulerPortraits_AllShareOneDimension()
        {
            AssertAllSameDimensions("Assets/Art/RulerPortraits");
        }

        [Test]
        public void Backgrounds_AllShareOneDimension()
        {
            AssertAllSameDimensions("Assets/Art/Backgrounds");
        }

        [Test]
        public void PanelArt_AllShareOneDimension()
        {
            AssertAllSameDimensions("Assets/Art/PanelArt");
        }

        [Test]
        public void ButtonIcons_AllShareOneDimension()
        {
            AssertAllSameDimensions("Assets/Art/ButtonIcons");
        }
    }
}
