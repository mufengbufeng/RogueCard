#if UNITY_EDITOR

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace RogueCard.Editor.DraftWorkbench.Tests
{
    /// <summary>
    /// 覆盖 VirtualRoot、透明 icon 切图、refinement 和 alpha gate 的回归测试。
    /// </summary>
    [TestFixture]
    public class DraftRootAndIconCutoutTests
    {
        private const string SourceHash = "root_icon_cutout_test";

        [TearDown]
        public void TearDown()
        {
            DeleteDirectory(DraftJsonRawCropService.DefaultRawCropRoot + "/" + SourceHash);
            DeleteDirectory(ComfyUiServiceConfig.DefaultCacheRoot + "/" + SourceHash);
            DeleteDirectory(DraftGenerationReportStore.DefaultReportRoot + "/" + SourceHash);
            DeleteAssetPath("Assets/AssetRaw/Image/DraftWorkbenchGenerated/TestPrefab/" + SourceHash);
        }

        [Test]
        public void Convert_DefaultVirtualRoot_DoesNotEmitJsonRootAndPreservesRootCoordinateParent()
        {
            var converter = new UiStructureConverter();
            var structure = CreateRootWithPanelAndIcon();

            List<ConvertedNode> result = converter.Convert(structure);

            Assert.AreEqual(UiStructureRootMode.VirtualRoot, UiStructureConversionOptions.Default.RootMode);
            Assert.AreEqual(2, result.Count);
            Assert.IsFalse(result.Any(node => node.Descriptor.Name == "LevelPreviewMenu"));
            Assert.AreEqual("Panel", result[0].Descriptor.Name);
            Assert.AreEqual(string.Empty, result[0].Descriptor.ParentPath);
            Assert.AreEqual("Panel/Icon", PrefabDraftBuilder.GetDescriptorPath(result[1].Descriptor));
            Assert.That(result[0].Descriptor.AnchoredPosition.x, Is.EqualTo(250f).Within(0.001f));
            Assert.That(result[0].Descriptor.AnchoredPosition.y, Is.EqualTo(-250f).Within(0.001f));
        }

        [Test]
        public void Convert_IncludeRootNode_CompatibilityKeepsOldFullHierarchy()
        {
            var converter = new UiStructureConverter();
            var structure = CreateRootWithPanelAndIcon();

            List<ConvertedNode> result = converter.Convert(structure, UiStructureConversionOptions.IncludeRootNode);

            Assert.AreEqual(3, result.Count);
            Assert.AreEqual("LevelPreviewMenu", result[0].Descriptor.Name);
            Assert.AreEqual(string.Empty, result[0].Descriptor.ParentPath);
            Assert.AreEqual("LevelPreviewMenu", result[1].Descriptor.ParentPath);
            Assert.AreEqual("LevelPreviewMenu/Panel", result[2].Descriptor.ParentPath);
        }

        [Test]
        public void Convert_PreservesExplicitAssetAndCropSemanticsOnDescriptorVisuals()
        {
            var converter = new UiStructureConverter();
            var structure = CreateRootWithPanelAndIcon();
            structure.root.children[0].children[0].assetKind = "icon";
            structure.root.children[0].children[0].alphaMode = "TransparentForeground";
            structure.root.children[0].children[0].paddingPixels = 18;
            structure.root.children[0].children[0].paddingPercent = 0.2f;
            structure.root.children[0].children[0].makeSquare = true;
            structure.root.children[0].children[0].requiresTransparentAlpha = true;
            structure.root.children[0].children[0].marker = "coin_marker";
            structure.root.children[0].children[0].spriteHint = "coin hint";
            structure.root.children[0].children[0].regionId = "r_coin";

            List<ConvertedNode> result = converter.Convert(structure);
            UiNodeVisuals visuals = result[1].Descriptor.Visuals;

            Assert.AreEqual(DraftAssetKind.Icon, visuals.AssetKind);
            Assert.AreEqual(DraftAlphaMode.TransparentForeground, visuals.AlphaMode);
            Assert.AreEqual(18, visuals.PaddingPixels);
            Assert.AreEqual(0.2f, visuals.PaddingPercent);
            Assert.IsTrue(visuals.MakeSquare);
            Assert.IsTrue(visuals.RequiresTransparentAlpha);
            Assert.AreEqual("coin_marker", visuals.AssetMarker);
            Assert.AreEqual("coin hint", visuals.SpriteHint);
            Assert.AreEqual("r_coin", visuals.RegionId);
        }

        [Test]
        public void PrefabApply_VirtualRoot_DoesNotCreateWrapperAndKeepsRootComponents()
        {
            var root = new GameObject("RootCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            try
            {
                var converter = new UiStructureConverter();
                var converted = converter.Convert(CreateRootWithPanelAndIcon());
                var changes = PrefabDraftBuilder.BuildPreview(root, converted.Select(node => node.Descriptor));

                PrefabDraftBuilder.ApplyChanges(root, changes);

                Assert.IsNull(root.transform.Find("LevelPreviewMenu"));
                Assert.IsNotNull(root.transform.Find("Panel"));
                Assert.AreEqual("RootCanvas", root.name);
                Assert.IsNotNull(root.GetComponent<Canvas>());
                Assert.IsNotNull(root.GetComponent<CanvasScaler>());
                Assert.IsNotNull(root.GetComponent<GraphicRaycaster>());
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void WrapperCleanup_RemovesOnlyRecordedTopLevelWrappers()
        {
            var root = new GameObject("RootCanvas");
            try
            {
                new GameObject("LevelPreviewMenu").transform.SetParent(root.transform, false);
                new GameObject("MainMenuScreen").transform.SetParent(root.transform, false);
                var metadata = ScriptableObject.CreateInstance<DraftWorkbenchMetadata>();
                metadata.ApplyRecords.Add(new DraftApplyRecord
                {
                    CreatedObjectPaths = new List<string> { "LevelPreviewMenu", "LevelPreviewMenu/Icon" }
                });

                DraftWrapperCleanupReport report = DraftWrapperCleanupService.CleanupRecordedWrappers(
                    root,
                    metadata,
                    new[] { "LevelPreviewMenu", "MainMenuScreen" });

                Assert.AreEqual(1, report.RemovedPaths.Count);
                Assert.AreEqual("LevelPreviewMenu", report.RemovedPaths[0]);
                Assert.IsNull(root.transform.Find("LevelPreviewMenu"));
                Assert.IsNotNull(root.transform.Find("MainMenuScreen"));
                Assert.AreEqual(1, report.UnrecordedCandidates.Count);

                Object.DestroyImmediate(metadata);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ExtractFromPreviewChanges_InfersIconTransparencyAndPanelOpaqueIntent()
        {
            var changes = new List<UguiNodeChange>
            {
                CreateImageChange("CoinIcon", "", new Rect(10, 10, 20, 20), null),
                CreateImageChange("LevelPreviewPanel", "", new Rect(0, 0, 160, 90), null)
            };

            var result = DraftJsonRegionExtractionService.ExtractFromPreviewChanges(changes, SourceHash, "Assets/Test/TestImg.jpg", 200, 100);

            DraftSegmentRegion icon = result.Manifest.regions[0];
            DraftSegmentRegion panel = result.Manifest.regions[1];
            Assert.AreEqual(DraftAssetKind.Icon, icon.assetKind);
            Assert.AreEqual(DraftAlphaMode.TransparentForeground, icon.alphaMode);
            Assert.IsTrue(icon.requiresTransparentAlpha);
            Assert.IsTrue(icon.makeSquare);
            Assert.AreEqual(DraftAssetKind.Panel, panel.assetKind);
            Assert.AreEqual(DraftAlphaMode.OpaqueRect, panel.alphaMode);
            Assert.IsFalse(panel.requiresTransparentAlpha);
        }

        [Test]
        public void ExtractFromPreviewChanges_SkipsImplicitCompositeButtonWhenChildImageRegionsExist()
        {
            var changes = new List<UguiNodeChange>
            {
                CreateButtonImageChange("ShopButton", "BottomNavigation", new Rect(33, 1160, 110, 184), null),
                CreateImageChange("ShopIcon", "BottomNavigation/ShopButton", new Rect(39, 1162, 95, 111), null)
            };

            var result = DraftJsonRegionExtractionService.ExtractFromPreviewChanges(changes, SourceHash, "Assets/Test/TestImg.jpg", 768, 1376);

            Assert.AreEqual(1, result.Manifest.regions.Count);
            DraftSegmentRegion region = result.Manifest.regions.Single();
            Assert.AreEqual("ShopIcon", region.label);
            Assert.AreEqual(DraftAssetKind.Icon, region.assetKind);
            Assert.AreEqual(DraftAlphaMode.TransparentForeground, region.alphaMode);
            Assert.IsTrue(result.SkippedDescriptors.Any(skip => skip.DescriptorPath == "BottomNavigation/ShopButton"));
        }

        [Test]
        public void ExtractFromPreviewChanges_JsonProvidedIntentOverridesHeuristics()
        {
            var visuals = new UiNodeVisuals
            {
                AssetKind = DraftAssetKind.Background,
                HasAssetKind = true,
                AlphaMode = DraftAlphaMode.OpaqueRect,
                HasAlphaMode = true,
                PaddingPixels = 3,
                HasPaddingPixels = true,
                PaddingPercent = 0.01f,
                HasPaddingPercent = true,
                MakeSquare = false,
                HasMakeSquare = true,
                RequiresTransparentAlpha = false,
                HasRequiresTransparentAlpha = true
            };
            var changes = new List<UguiNodeChange>
            {
                CreateImageChange("CoinIcon", "", new Rect(10, 10, 20, 20), visuals)
            };

            var result = DraftJsonRegionExtractionService.ExtractFromPreviewChanges(changes, SourceHash, "Assets/Test/TestImg.jpg", 200, 100);
            DraftSegmentRegion region = result.Manifest.regions.Single();

            Assert.AreEqual(DraftAssetKind.Background, region.assetKind);
            Assert.AreEqual(DraftAlphaMode.OpaqueRect, region.alphaMode);
            Assert.AreEqual(3, region.paddingPixels);
            Assert.AreEqual(0.01f, region.paddingPercent);
            Assert.IsFalse(region.makeSquare);
            Assert.IsFalse(region.requiresTransparentAlpha);
        }

        [Test]
        public void GenerateRawCrops_TransparentIconExpandsAndWritesSquareTransparentCanvas()
        {
            var source = CreateSourceTexture(10, 8);
            var region = new DraftSegmentRegion
            {
                id = "r001",
                marker = "coin",
                label = "CoinIcon",
                assetKind = DraftAssetKind.Icon,
                alphaMode = DraftAlphaMode.TransparentForeground,
                requiresTransparentAlpha = true,
                makeSquare = true,
                cropBounds = new DraftSegmentBounds { x = 4, y = 3, width = 2, height = 2 },
                bbox = new DraftSegmentBounds { x = 4, y = 3, width = 2, height = 2 },
                included = true,
                reviewState = DraftRegionReviewState.Included
            };
            var manifest = new DraftSegmentationManifest { sourceHash = SourceHash, sourceWidth = 10, sourceHeight = 8, regions = new List<DraftSegmentRegion> { region } };
            var config = ScriptableObject.CreateInstance<ComfyUiServiceConfig>();
            config.RegionExpandPixels = 1;
            config.RegionExpandPercent = 0f;

            DraftJsonRawCropService.GenerateRawCrops(source, manifest, config);

            Assert.AreEqual(new Rect(3, 2, 4, 4), region.expandedBbox.ToRect());
            Assert.AreEqual(4, region.rawOutputWidth);
            Assert.AreEqual(4, region.rawOutputHeight);
            Assert.AreEqual(new Rect(4, 3, 2, 2), region.cropBounds.ToRect(), "UI placement bounds 不应被扩边覆盖。");
            Assert.IsTrue(File.Exists(DraftPathUtility.ToAbsolutePath(region.rawPngPath)));

            Object.DestroyImmediate(config);
            Object.DestroyImmediate(source);
        }

        [Test]
        public void BuildTransparentPrompts_UsesBiRefNetDefaultAndSam2PromptGeometry()
        {
            var region = new DraftSegmentRegion
            {
                cropBounds = new DraftSegmentBounds { x = 10, y = 20, width = 30, height = 40 },
                expandedBbox = new DraftSegmentBounds { x = 5, y = 15, width = 50, height = 60 }
            };

            string birefnet = ComfyWorkflowAdapter.BuildTransparentRefinementApiPromptJson("raw.png", "DW_icon");
            string sam2 = ComfyWorkflowAdapter.BuildSam2RefinementApiPromptJson("raw.png", "DW_icon_sam2", region);

            StringAssert.Contains("BiRefNet", birefnet);
            StringAssert.Contains("SaveImage", birefnet);
            StringAssert.Contains("SAM2", sam2);
            StringAssert.Contains("\"bbox\"", sam2);
            StringAssert.Contains("\"point\"", sam2);
        }

        [Test]
        public async Task RefineSelectedRawCrops_TransparentIconBlocksOpaqueOutputWithoutLocalFallback()
        {
            string rawRelativePath = DraftJsonRawCropService.DefaultRawCropRoot + "/" + SourceHash + "/r001_icon_coin.png";
            WritePng(DraftPathUtility.ToAbsolutePath(rawRelativePath), 2, 2, new Color32(255, 128, 64, 255));
            var region = new DraftSegmentRegion
            {
                id = "r001",
                marker = "icon_coin",
                label = "CoinIcon",
                rawPngPath = rawRelativePath,
                cropBounds = new DraftSegmentBounds { x = 0, y = 0, width = 2, height = 2 },
                assetKind = DraftAssetKind.Icon,
                alphaMode = DraftAlphaMode.TransparentForeground,
                requiresTransparentAlpha = true,
                included = true,
                reviewState = DraftRegionReviewState.Included
            };
            var manifest = new DraftSegmentationManifest { sourceHash = SourceHash, regions = new List<DraftSegmentRegion> { region } };
            var config = ScriptableObject.CreateInstance<ComfyUiServiceConfig>();
            config.EnableLocalTransparentFallback = false;
            var transport = new TestComfyUiTransport
            {
                OutputBytes = CreatePngBytes(2, 2, new Color32(255, 128, 64, 255))
            };

            List<DraftCropRefinementRecord> records = await DraftComfyCropRefinementService.RefineSelectedRawCropsAsync(
                manifest,
                config,
                transport);

            StringAssert.Contains("BiRefNet", transport.LastPromptJson);
            Assert.AreEqual(DraftRefinementWorkflowKind.BiRefNetRmbg, records[0].WorkflowKind);
            Assert.AreEqual(DraftRegionGenerationStatus.Failed, region.generationStatus);
            Assert.AreEqual(DraftRegionReviewState.NeedsReview, region.reviewState);
            Assert.IsNotNull(region.alphaValidation);
            Assert.AreEqual(0f, region.alphaValidation.TransparentRatio);
            StringAssert.Contains("opaque", region.generationMessage.ToLowerInvariant());
            Assert.AreEqual(1, region.refinementAttempts.Count);
            Object.DestroyImmediate(config);
        }

        [Test]
        public async Task RefineSelectedRawCrops_TransparentIconUsesLocalCutoutWhenComfyFails()
        {
            string rawRelativePath = DraftJsonRawCropService.DefaultRawCropRoot + "/" + SourceHash + "/r002_icon_shop.png";
            WriteIconRawPng(DraftPathUtility.ToAbsolutePath(rawRelativePath));
            var region = new DraftSegmentRegion
            {
                id = "r002",
                marker = "icon_shop",
                label = "ShopIcon",
                rawPngPath = rawRelativePath,
                cropBounds = new DraftSegmentBounds { x = 0, y = 0, width = 5, height = 5 },
                assetKind = DraftAssetKind.Icon,
                alphaMode = DraftAlphaMode.TransparentForeground,
                requiresTransparentAlpha = true,
                included = true,
                reviewState = DraftRegionReviewState.Included
            };
            var manifest = new DraftSegmentationManifest { sourceHash = SourceHash, regions = new List<DraftSegmentRegion> { region } };
            var config = ScriptableObject.CreateInstance<ComfyUiServiceConfig>();
            var transport = new TestComfyUiTransport { FailOnQueuePrompt = true };

            List<DraftCropRefinementRecord> records = await DraftComfyCropRefinementService.RefineSelectedRawCropsAsync(
                manifest,
                config,
                transport);

            DraftCropRefinementRecord localAttempt = records.Single(record => record.WorkflowKind == DraftRefinementWorkflowKind.LocalBackgroundCutout);
            Assert.AreEqual(DraftRegionGenerationStatus.Generated, region.generationStatus);
            Assert.AreEqual(localAttempt.RefinedPngPath, region.refinedPngPath);
            Assert.IsTrue(File.Exists(DraftPathUtility.ToAbsolutePath(region.refinedPngPath)));
            Assert.AreEqual(DraftAlphaValidationStatus.Passed, region.alphaValidation.Status);
            Assert.Greater(region.alphaValidation.TransparentRatio, 0f);
            Assert.Less(region.alphaValidation.BorderOpaqueRatio, 0.95f);

            Object.DestroyImmediate(config);
        }

        [Test]
        public void LocalTransparentCutout_KeepsOffCenterSubjectAndClearsConnectedBackground()
        {
            var source = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            var pixels = new Color32[64];
            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    int borderDistance = Mathf.Min(Mathf.Min(x, y), Mathf.Min(7 - x, 7 - y));
                    byte shade = (byte)(42 + borderDistance * 40);
                    pixels[y * 8 + x] = new Color32(shade, (byte)(shade + 12), (byte)(shade + 24), 255);
                }
            }

            for (int y = 1; y <= 4; y++)
            {
                for (int x = 1; x <= 4; x++)
                {
                    pixels[y * 8 + x] = new Color32(224, 84, 38, 255);
                }
            }

            source.SetPixels32(pixels);
            source.Apply(false, false);
            var config = ScriptableObject.CreateInstance<ComfyUiServiceConfig>();
            config.LocalCutoutColorDistance = 72f;

            Texture2D cutout = DraftLocalTransparentCutoutService.CreateCutoutTexture(source, config, out string message);

            Assert.IsNotNull(cutout, message);
            Color32[] output = cutout.GetPixels32();
            Assert.AreEqual(255, output[1 * 8 + 1].a, "偏心主体左下角不应被中心兜底裁掉。");
            Assert.AreEqual(255, output[4 * 8 + 4].a, "主体右上角应被保留。");
            Assert.AreEqual(0, output[7 * 8 + 7].a, "边缘连通背景应被抠除。");
            Assert.AreEqual(0, output[0 * 8 + 7].a, "多色边缘背景应被抠除。");
            Assert.AreEqual(0, output[5 * 8 + 5].a, "与边缘连通但颜色变化大的内部背景也应被抠除。");

            Object.DestroyImmediate(cutout);
            Object.DestroyImmediate(config);
            Object.DestroyImmediate(source);
        }

        [Test]
        public void AlphaValidation_ComputesStatsAndRejectsOpaqueTransparentIcon()
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.SetPixels32(new[]
            {
                new Color32(255, 0, 0, 255), new Color32(255, 0, 0, 255),
                new Color32(255, 0, 0, 255), new Color32(255, 0, 0, 255)
            });
            texture.Apply(false, false);

            DraftAlphaValidationResult stats = DraftAlphaValidationService.Validate(texture, true, ScriptableObject.CreateInstance<ComfyUiServiceConfig>());

            Assert.AreEqual(255, stats.MinAlpha);
            Assert.AreEqual(255, stats.MaxAlpha);
            Assert.AreEqual(0f, stats.TransparentRatio);
            Assert.AreEqual(DraftAlphaValidationStatus.Failed, stats.Status);
            StringAssert.Contains("transparent", stats.Message.ToLowerInvariant());

            Object.DestroyImmediate(texture);
        }

        [Test]
        public void ImportRefinedSprites_TransparentIconWithoutAlphaIsNotCopiedToFinalOutput()
        {
            string refinedRelativePath = ComfyUiServiceConfig.DefaultCacheRoot + "/" + SourceHash + "/refined/r001_icon_coin_refined.png";
            WritePng(DraftPathUtility.ToAbsolutePath(refinedRelativePath), 3, 3, new Color32(0, 255, 0, 255));
            var region = new DraftSegmentRegion
            {
                id = "r001",
                marker = "icon_coin",
                label = "CoinIcon",
                refinedPngPath = refinedRelativePath,
                assetKind = DraftAssetKind.Icon,
                alphaMode = DraftAlphaMode.TransparentForeground,
                requiresTransparentAlpha = true,
                included = true,
                reviewState = DraftRegionReviewState.Included
            };
            var manifest = new DraftSegmentationManifest { sourceHash = SourceHash, regions = new List<DraftSegmentRegion> { region } };
            var config = ScriptableObject.CreateInstance<ComfyUiServiceConfig>();

            DraftSpriteImportReport report = DraftRefinedSpriteImportService.ImportRefinedSprites(manifest, config, "TestPrefab");

            string finalPath = "Assets/AssetRaw/Image/DraftWorkbenchGenerated/TestPrefab/" + SourceHash + "/DW_" + SourceHash + "_r001_icon_coin.png";
            Assert.AreEqual(0, report.GeneratedSprites.Count);
            Assert.IsFalse(File.Exists(DraftPathUtility.ToAbsolutePath(finalPath)));
            Assert.AreEqual(DraftRegionGenerationStatus.Failed, region.generationStatus);
            Assert.AreEqual(DraftRegionReviewState.NeedsReview, region.reviewState);
            Assert.IsNotNull(region.alphaValidation);
            StringAssert.Contains("alpha", string.Join("\n", report.Messages).ToLowerInvariant());

            Object.DestroyImmediate(config);
        }

        [Test]
        public void GenerationReport_TransparentFailureIncludesAlphaStats()
        {
            var region = new DraftSegmentRegion
            {
                id = "r001",
                marker = "icon_coin",
                label = "CoinIcon",
                assetKind = DraftAssetKind.Icon,
                alphaMode = DraftAlphaMode.TransparentForeground,
                requiresTransparentAlpha = true,
                alphaValidation = new DraftAlphaValidationResult
                {
                    Status = DraftAlphaValidationStatus.Failed,
                    MinAlpha = 255,
                    MaxAlpha = 255,
                    TransparentRatio = 0f,
                    BorderOpaqueRatio = 1f,
                    NonOpaquePixelRatio = 0f,
                    Message = "opaque output"
                },
                generationStatus = DraftRegionGenerationStatus.Failed,
                generationMessage = "opaque output"
            };
            var manifest = new DraftSegmentationManifest { sourceHash = SourceHash, regions = new List<DraftSegmentRegion> { region } };

            DraftGenerationReportStore.SaveSnapshot(manifest, null, new DraftSpriteImportReport());

            string reportPath = DraftPathUtility.ToAbsolutePath(DraftGenerationReportStore.DefaultReportRoot + "/" + SourceHash + "/generation-report.json");
            var document = JsonUtility.FromJson<DraftGenerationReportDocument>(File.ReadAllText(reportPath));
            Assert.AreEqual(DraftAlphaValidationStatus.Failed, document.RawRegions[0].alphaValidation.Status);
            Assert.AreEqual(0f, document.RawRegions[0].alphaValidation.TransparentRatio);
            Assert.AreEqual(DraftAlphaMode.TransparentForeground, document.RawRegions[0].alphaMode);
        }

        [Test]
        public async Task EndToEnd_TestImgJsonFlow_GeneratesTransparentIconsInStandaloneOutput()
        {
            const string prefabAssetPath = "Assets/AssetRaw/UI/Main/MainTest.prefab";
            var maintenance = DraftMaintenanceRecordStore.Load(prefabAssetPath);
            Assert.IsNotNull(maintenance, "应能加载 MainTest 的维护记录。");
            Assert.IsNotNull(maintenance.SourceImage);
            Assert.IsFalse(string.IsNullOrWhiteSpace(maintenance.SourceImage.AssetPath));
            Assert.IsNotEmpty(maintenance.Entries);

            DraftMaintenanceEntry latest = maintenance.Entries.LastOrDefault(entry => !string.IsNullOrWhiteSpace(entry.SourceJson));
            Assert.IsNotNull(latest, "维护记录中应包含可重放的源 JSON。");

            var sourceImage = AssetDatabase.LoadAssetAtPath<Texture2D>(maintenance.SourceImage.AssetPath);
            Assert.IsNotNull(sourceImage, "应能加载 TestImg.jpg 作为源图。");

            string sourceHash = DraftImageSegmentationService.ComputeSourceHash(sourceImage);
            var config = ScriptableObject.CreateInstance<ComfyUiServiceConfig>();
            config.EnableLocalTransparentFallback = true;
            config.EnableSam2Fallback = false;

            DraftJsonRegionExtractionResult extraction = DraftJsonRegionExtractionService.ExtractFromJson(
                latest.SourceJson,
                sourceHash,
                maintenance.SourceImage.AssetPath,
                sourceImage.width,
                sourceImage.height);
            Assert.IsFalse(extraction.HasError, extraction.ErrorMessage);
            Assert.Greater(extraction.Manifest.regions.Count, 0);

            DraftJsonRawCropService.GenerateRawCrops(sourceImage, extraction.Manifest, config);
            var transport = new TestComfyUiTransport { FailOnQueuePrompt = true };
            await DraftComfyCropRefinementService.RefineSelectedRawCropsAsync(extraction.Manifest, config, transport);
            DraftSpriteImportReport report = DraftRefinedSpriteImportService.ImportRefinedSprites(extraction.Manifest, config, null);

            Assert.IsNotEmpty(report.GeneratedSprites);
            Assert.That(report.OutputRoot, Does.Contain("Assets/AssetRaw/Image/DraftWorkbenchGenerated/Standalone/" + sourceHash));

            var transparentSprites = report.GeneratedSprites
                .Where(sprite => sprite.AlphaMode == DraftAlphaMode.TransparentForeground)
                .ToList();
            Assert.IsNotEmpty(transparentSprites, "应至少生成一个透明前景 icon。");

            foreach (var sprite in transparentSprites)
            {
                Assert.AreEqual(DraftAlphaValidationStatus.Passed, sprite.AlphaValidation.Status, sprite.Marker);
                Assert.Greater(sprite.AlphaValidation.TransparentRatio, 0f, sprite.Marker);
                Assert.Less(sprite.AlphaValidation.BorderOpaqueRatio, 0.95f, sprite.Marker);
                Assert.IsTrue(File.Exists(DraftPathUtility.ToAbsolutePath(sprite.SpriteAssetPath)), sprite.Marker);
            }

            Object.DestroyImmediate(config);
        }

        private static UiStructure CreateRootWithPanelAndIcon()
        {
            return new UiStructure
            {
                canvas = new UiCanvas { width = 768, height = 1376 },
                root = new UiElement
                {
                    type = "container",
                    name = "LevelPreviewMenu",
                    position = new UiPosition { x = 10, y = 20 },
                    size = new UiSize { width = 500, height = 600 },
                    children = new List<UiElement>
                    {
                        new UiElement
                        {
                            type = "container",
                            name = "Panel",
                            position = new UiPosition { x = 60, y = 120 },
                            size = new UiSize { width = 400, height = 300 },
                            children = new List<UiElement>
                            {
                                new UiElement
                                {
                                    type = "image",
                                    name = "Icon",
                                    position = new UiPosition { x = 100, y = 150 },
                                    size = new UiSize { width = 40, height = 40 }
                                }
                            }
                        }
                    }
                }
            };
        }

        private static UguiNodeChange CreateImageChange(string name, string parentPath, Rect bounds, UiNodeVisuals visuals)
        {
            return new UguiNodeChange
            {
                Descriptor = new UguiNodeDescriptor
                {
                    Name = name,
                    ParentPath = parentPath,
                    ComponentTypeNames = new List<string> { "UnityEngine.UI.Image" },
                    HasSourceBounds = true,
                    SourceBounds = bounds,
                    SourceCanvasSize = new Vector2(200, 100),
                    Visuals = visuals
                }
            };
        }

        private static UguiNodeChange CreateButtonImageChange(string name, string parentPath, Rect bounds, UiNodeVisuals visuals)
        {
            return new UguiNodeChange
            {
                Descriptor = new UguiNodeDescriptor
                {
                    Name = name,
                    ParentPath = parentPath,
                    ComponentTypeNames = new List<string> { "UnityEngine.UI.Image", "UnityEngine.UI.Button" },
                    HasSourceBounds = true,
                    SourceBounds = bounds,
                    SourceCanvasSize = new Vector2(768, 1376),
                    Visuals = visuals
                }
            };
        }

        private static Texture2D CreateSourceTexture(int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(255, 128, 64, 255);
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        private static void WritePng(string absolutePath, int width, int height, Color32 color)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
            File.WriteAllBytes(absolutePath, CreatePngBytes(width, height, color));
        }

        private static void WriteIconRawPng(string absolutePath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
            var texture = new Texture2D(5, 5, TextureFormat.RGBA32, false);
            var pixels = new Color32[25];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(40, 170, 70, 255);
            }

            for (int y = 1; y <= 3; y++)
            {
                for (int x = 1; x <= 3; x++)
                {
                    pixels[y * 5 + x] = new Color32(220, 80, 40, 255);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            File.WriteAllBytes(absolutePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }

        private static byte[] CreatePngBytes(int width, int height, Color32 color)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            byte[] bytes = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);
            return bytes;
        }

        private static void DeleteDirectory(string relativePath)
        {
            string absolutePath = DraftPathUtility.ToAbsolutePath(relativePath);
            if (Directory.Exists(absolutePath))
            {
                Directory.Delete(absolutePath, true);
            }
        }

        private static void DeleteAssetPath(string assetPath)
        {
            if (AssetDatabase.LoadAssetAtPath<Object>(assetPath) != null)
            {
                AssetDatabase.DeleteAsset(assetPath);
            }

            string absolutePath = DraftPathUtility.ToAbsolutePath(assetPath);
            string directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }
}

#endif
