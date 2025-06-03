using System.Diagnostics;
using OnixRuntime.Api;
using OnixRuntime.Api.Entities;
using OnixRuntime.Api.Maths;
using OnixRuntime.Api.NBT;
using OnixRuntime.Plugin;
using OnixRuntime.Api.Rendering;
using OnixRuntime.Api.World;
using OnixRuntime.Api.Utils;

namespace VisualJukebox {
    public class VisualJukebox : OnixPluginBase {
        public VisualJukebox(OnixPluginInitInfo initInfo) : base(initInfo) {
#if DEBUG
#endif
        }

        protected override void OnLoaded() {
            Onix.Events.Common.WorldRender += OnWorldRender;
            Onix.Events.Common.Tick += OnTick;
        }

        private readonly Dictionary<BoundingBox, Dictionary<string, int?>> _jukeboxPositions = [];
        private WorldChunk.FoundBlock[] _jukeboxBlocks = [];
        private float _rotationAngle;
        private float _discSize = 0.7f;
        private float _glassSize = 0.8f;

        private void OnTick() {
            _jukeboxPositions.Clear();
            LocalPlayer player = Onix.LocalPlayer!;
            ChunkPos chunkPos = player.ChunkPosition;
            chunkPos.X -= 2;
            chunkPos.Y -= 2;

            Block? jukeboxBlock = Onix.World?.BlockRegistry.GetBlock("jukebox");
            if (jukeboxBlock == null) return;
            for (int x = 0; x < 5; x++) {
                for (int z = 0; z < 5; z++) {
                    WorldChunk? chunk = player.Region.GetChunk(new ChunkPos(chunkPos.X + x, chunkPos.Y + z));
                    if (chunk is null) continue;
                    _jukeboxBlocks = chunk.FindBlocks(jukeboxBlock, false);
                    foreach (WorldChunk.FoundBlock foundBlock in _jukeboxBlocks) {
                        BlockPos blockPos = foundBlock.Position;
                        BoundingBox box = blockPos.BoundingBox;
                        BlockEntity? blockEntity = player.Dimension.Region.GetBlockEntity(blockPos);
                        ObjectTag? obj = blockEntity?.SaveToNbt();
                        Dictionary<string, NbtTag>? tag = obj?.Value;
                        if (tag == null) continue;
                        if (!tag.TryGetValue("RecordItem", out NbtTag? recordTag)) continue;
                        if (recordTag is not ObjectTag recordObject) continue;
                        foreach (KeyValuePair<string, NbtTag> kvp in recordObject.Value) {
                            if (kvp is not { Key: "Name", Value: StringTag nameTag }) continue;
                            string recordName = nameTag.Value;
                            int? time = Onix.World?.CurrentTime;
                            int brightness = SunBrightness(time ?? 0);
                            _jukeboxPositions.Add(box, new Dictionary<string, int?> {
                                { recordName, brightness }
                            });
                        }
                    }
                }
            }
        }

        private static int SunBrightness(float time) {
            float t = Math.Abs(time - 0.5f);

            if (t < 0.2f) return 90;
            if (t > 0.3f) return 255;
            return (int)Math.Round(165f / 0.1f * (t - 0.2f) + 90f);
        }

        private void OnWorldRender(RendererWorld gfx, float delta) {
            float spinSpeed = 0.05f; // 0.2f
            _rotationAngle += spinSpeed * delta;
            _rotationAngle %= (2.0f * (float)Math.PI);
            
            
            foreach (BoundingBox box in _jukeboxPositions.Keys) {
                float extrusionDepth = 0.06f * _discSize;
                gfx.EnableLights(false);
                gfx.SetupLights(box);
                if (_jukeboxPositions.TryGetValue(box, out Dictionary<string, int?>? jukeboxData)) {
                    BoundingBox newBox = box;
                    BoundingBox boxBlock = box;
                    Vec3 boxCenter = boxBlock.Center;

                    Vec3 center = newBox.Center;
                    center.Y += 0.525f;
                    float bounceSpeed = 0f;
                    float bounceHeight = 0.000f;
                    float time = (float)Stopwatch.GetTimestamp() / Stopwatch.Frequency;
                    float bounce = (float)Math.Sin(time * bounceSpeed) * bounceHeight;
                    center.Y += bounce;
                    using (var settings = gfx.PushWorldRenderSettings(true, true)) {

                        Vec3 right = new Vec3((float)Math.Cos(_rotationAngle), 0, (float)Math.Sin(_rotationAngle)) * 0.5f * _discSize;
                        Vec3 forward = new Vec3((float)-Math.Sin(_rotationAngle), 0, (float)Math.Cos(_rotationAngle)) * 0.5f * _discSize;

                        string? jukeboxName = jukeboxData.Keys.ElementAtOrDefault(0);

                        string? textureName = jukeboxName?.Replace("minecraft:music_disc_", "record_");
                        if (textureName == null) continue;
                        if (textureName == "record_creator")
                            textureName = "music_disc_creator";
                        else if (textureName == "record_creator_music_box")
                            textureName = "music_disc_creator_music_box";
                        else if (textureName == "record_precipice")
                            textureName = "music_disc_precipice";
                        else if (textureName == "record_relic")
                            textureName = "music_disc_relic";
                        using var mb = gfx.NewMeshBuilderSession(TexturePath.Game("textures/items/" + textureName),
                            MeshBuilderPrimitiveType.Quad, ColorF.White);
                        if (gfx.GetTextureStatus(TexturePath.Game("textures/items/" + textureName)) !=
                            RendererTextureStatus.Loaded) {
                            continue;
                        }

                        byte[] pngDataOfThatTexture =
                            Onix.Game.PackManager.LoadContent(
                                TexturePath.Game("textures/items/" + textureName + ".png"));
                        RawImageData imageData = RawImageData.Load(pngDataOfThatTexture);
                        int width = imageData.Width;
                        int height = imageData.Height;
                        bool[] edges = new bool[width * height];
                        for (int y = 0; y < height; y++) {
                            for (int x = 0; x < width; x++) {
                                ColorF pixel = imageData.GetPixel(x, y);
                                if (pixel.A != 0) {
                                    if (x == 0) {
                                        edges[y * width] = true;
                                    }

                                    if (y == 0) {
                                        edges[x * width] = true;
                                    }

                                    if (x == width - 1) {
                                        edges[y * width + (width - 1)] = true;
                                    }

                                    if (y == height - 1) {
                                        edges[(height - 1) * width + x] = true;
                                    }

                                    continue;
                                }

                                if (x != 0 && imageData.GetPixel(x - 1, y).A != 0) {
                                    edges[y * width + (x - 1)] = true;
                                }

                                if (x != width - 1 && imageData.GetPixel(x + 1, y).A != 0) {
                                    edges[y * width + x + 1] = true;
                                }

                                if (y != 0 && imageData.GetPixel(x, y - 1).A != 0) {
                                    edges[(y - 1) * width + x] = true;
                                }

                                if (y != height - 1 && imageData.GetPixel(x, y + 1).A != 0) {
                                    edges[(y + 1) * width + x] = true;
                                }
                            }
                        }

                        Vec3 bl = center - right - forward;
                        Vec3 tl = center - right + forward;
                        Vec3 br = center + right - forward;
                        Vec3 tr = center + right + forward;

                        float pixelToWorldScale = (2f / Math.Max(width, height));

                        float heightOffset = 0.03f;

                        for (int y = 0; y < height; y++) {
                            for (int x = 0; x < width; x++) {
                                if (!edges[y * width + x]) continue;

                                float normalizedX = ((x + 0.5f - width / 2.0f) * pixelToWorldScale);
                                float normalizedY = (y + 0.5f - height / 2.0f) * pixelToWorldScale;

                                Vec3 pixelWorldPos = center + (right * normalizedX) + (forward * normalizedY);

                                float pixelSize = pixelToWorldScale * 0.5f;

                                Vec3 topOffset = new(0, heightOffset, 0);
                                Vec3 topBl = pixelWorldPos - (right * pixelSize) - (forward * pixelSize) + topOffset;
                                Vec3 topTl = pixelWorldPos - (right * pixelSize) + (forward * pixelSize) + topOffset;
                                Vec3 topBr = pixelWorldPos + (right * pixelSize) - (forward * pixelSize) + topOffset;
                                Vec3 topTr = pixelWorldPos + (right * pixelSize) + (forward * pixelSize) + topOffset;

                                Vec3 bottomOffset = new(0, -extrusionDepth, 0);
                                Vec3 bottomBl = topBl + bottomOffset;
                                Vec3 bottomTl = topTl + bottomOffset;
                                Vec3 bottomBr = topBr + bottomOffset;
                                Vec3 bottomTr = topTr + bottomOffset;

                                float u = (float)x / width;
                                float v = (float)y / height;
                                float uSize = 1.0f / width;
                                float vSize = 1.0f / height;

                                float uCenter = u + uSize * 0.5f;
                                float vCenter = v + vSize * 0.5f;

                                Vec2 topLeftUv = new(uCenter, vCenter);
                                Vec2 topRightUv = new(uCenter, vCenter);
                                Vec2 bottomLeftUv = new(uCenter, vCenter);
                                Vec2 bottomRightUv = new(uCenter, vCenter);

                                // Left face
                                Vec4 leftNormal = new(-1, 0, 0, 0);
                                mb.Builder.Color(new ColorF(175, 175, 175));
                                mb.Builder.AddQuadUvNormalVertices(
                                    topTl, topLeftUv,
                                    topBl, topRightUv,
                                    bottomTl, bottomLeftUv,
                                    bottomBl, bottomRightUv,
                                    leftNormal
                                );
                                mb.Builder.Color(ColorF.White);

                                // Right face
                                Vec4 rightNormal = new(1, 0, 0, 0);
                                mb.Builder.Color(new ColorF(175, 175, 175));
                                mb.Builder.AddQuadUvNormalVertices(
                                    topBr, topLeftUv,
                                    topTr, topRightUv,
                                    bottomBr, bottomLeftUv,
                                    bottomTr, bottomRightUv,
                                    rightNormal
                                );
                                mb.Builder.Color(ColorF.White);

                                // Bottom face
                                Vec4 bottomNormal = new(0, 0, 1, 0);
                                mb.Builder.Color(new ColorF(150, 150, 150));
                                mb.Builder.AddQuadUvNormalVertices(
                                    topTl, topLeftUv,
                                    topTr, topRightUv,
                                    bottomTl, bottomLeftUv,
                                    bottomTr, bottomRightUv,
                                    bottomNormal
                                );
                                mb.Builder.Color(ColorF.White);

                                // Top face
                                Vec4 topNormal = new(0, 0, -1, 0);
                                mb.Builder.Color(new ColorF(150, 150, 150));
                                mb.Builder.AddQuadUvNormalVertices(
                                    topBl, topLeftUv,
                                    topBr, topRightUv,
                                    bottomBl, bottomLeftUv,
                                    bottomBr, bottomRightUv,
                                    topNormal
                                );
                                mb.Builder.Color(ColorF.White);
                            }
                        }

                        float yOffset = 0.0f;
                        Vec3 blOffset = bl;
                        blOffset.Y += yOffset;
                        Vec3 tlOffset = tl;
                        tlOffset.Y += yOffset;
                        Vec3 brOffset = br;
                        brOffset.Y += yOffset;
                        Vec3 trOffset = tr;
                        trOffset.Y += yOffset;

                        Vec3 topOffset2 = new(0, heightOffset - 0.0001f, 0);
                        Vec4 topNormal2 = new(0, 1, 0, 0);
                        mb.Builder.AddQuadUvNormalVertices(
                            tlOffset + topOffset2, new Vec2(0, 1),
                            blOffset + topOffset2, new Vec2(0, 0),
                            trOffset + topOffset2, new Vec2(1, 1),
                            brOffset + topOffset2, new Vec2(1, 0),
                            topNormal2
                        );
                        mb.Builder.Color(ColorF.White);
                        mb.Dispose();
                        gfx.FlushMesh();
                    }

                    using (var settings = gfx.PushWorldRenderSettings(true, true, false, false)) {

                        boxCenter.Y += 0.69f;
                        using var glassMb = gfx.NewMeshBuilderSession(TexturePath.Game("textures/blocks/glass"));

                        if (gfx.GetTextureStatus(TexturePath.Game("textures/blocks/glass")) !=
                            RendererTextureStatus.Loaded) {
                            continue;
                        }

                        Vec3 glassRight =
                            new Vec3((float)Math.Cos(_rotationAngle * 0), 0, (float)Math.Sin(_rotationAngle * 0)) *
                            0.5f *
                            _glassSize;
                        Vec3 glassForward =
                            new Vec3((float)-Math.Sin(_rotationAngle * 0), 0, (float)Math.Cos(_rotationAngle * 0)) *
                            0.5f *
                            _glassSize;

                        Vec3 bottomLeft = boxCenter - glassRight - glassForward;
                        Vec3 topLeft = boxCenter - glassRight + glassForward;
                        Vec3 bottomRight = boxCenter + glassRight - glassForward;
                        Vec3 topRight = boxCenter + glassRight + glassForward;

                        boxCenter.Y += 0.525f;
                        float myYOffset = 0.0f;
                        Vec3 bottomLeftOffset = bottomLeft;
                        bottomLeftOffset.Y += myYOffset;
                        Vec3 topLeftOffset = topLeft;
                        topLeftOffset.Y += myYOffset;
                        Vec3 bottomRightOffset = bottomRight;
                        bottomRightOffset.Y += myYOffset;
                        Vec3 topRightOffset = topRight;
                        topRightOffset.Y += myYOffset;

                        float glassHeight = _glassSize;
                        Vec3 bottomLeftBottom = bottomLeftOffset;
                        bottomLeftBottom.Y -= glassHeight;
                        Vec3 topLeftBottom = topLeftOffset;
                        topLeftBottom.Y -= glassHeight;
                        Vec3 bottomRightBottom = bottomRightOffset;
                        bottomRightBottom.Y -= glassHeight;
                        Vec3 topRightBottom = topRightOffset;
                        topRightBottom.Y -= glassHeight;
                        
                        // Top face
                        Vec3 topOffset3 = new(0, myYOffset - 0.0001f, 0);
                        glassMb.Builder.Color(ColorF.White);
                        glassMb.Builder.AddQuadUvVertices(
                            topLeftOffset + topOffset3, new Vec2(0, 1),
                            topRightOffset + topOffset3, new Vec2(1, 1),
                            bottomLeftOffset + topOffset3, new Vec2(0, 0),
                            bottomRightOffset + topOffset3, new Vec2(1, 0)
                        );
                        glassMb.Builder.Color(ColorF.White);

                        // Front face
                        glassMb.Builder.Color(new ColorF(175, 175, 175));
                        glassMb.Builder.AddQuadUvVertices(
                            topLeftOffset, new Vec2(0, 0),
                            topLeftBottom, new Vec2(0, 1),
                            topRightOffset, new Vec2(1, 0),
                            topRightBottom, new Vec2(1, 1)
                        );
                        glassMb.Builder.Color(ColorF.White);

                        // Back face
                        glassMb.Builder.Color(new ColorF(175, 175, 175));
                        glassMb.Builder.AddQuadUvVertices(
                            bottomRightOffset, new Vec2(0, 0),
                            bottomRightBottom, new Vec2(0, 1),
                            bottomLeftOffset, new Vec2(1, 0),
                            bottomLeftBottom, new Vec2(1, 1)
                        );
                        glassMb.Builder.Color(ColorF.White);

                        // Left face
                        glassMb.Builder.Color(new ColorF(200, 200, 200));
                        glassMb.Builder.AddQuadUvVertices(
                            bottomLeftOffset, new Vec2(0, 0),
                            bottomLeftBottom, new Vec2(0, 1),
                            topLeftOffset, new Vec2(1, 0),
                            topLeftBottom, new Vec2(1, 1)
                        );
                        glassMb.Builder.Color(ColorF.White);

                        // Right face
                        glassMb.Builder.Color(new ColorF(200, 200, 200));
                        glassMb.Builder.AddQuadUvVertices(
                            topRightOffset, new Vec2(0, 0),
                            topRightBottom, new Vec2(0, 1),
                            bottomRightOffset, new Vec2(1, 0),
                            bottomRightBottom, new Vec2(1, 1)
                        );
                        glassMb.Builder.Color(ColorF.White);
                        glassMb.Dispose();
                        gfx.FlushMesh();
                    }
                }
            }
        }

        protected override void OnUnloaded() {
            Onix.Events.Common.WorldRender -= OnWorldRender;
        }
    }
}