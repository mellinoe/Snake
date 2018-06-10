using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Veldrid;
using Veldrid.ImageSharp;
using Veldrid.SPIRV;

namespace Snake
{
    public class SpriteRenderer
    {
        private readonly List<SpriteInfo> _draws = new List<SpriteInfo>();

        private DeviceBuffer _vertexBuffer;
        private DeviceBuffer _orthoBuffer;
        private ResourceLayout _orthoLayout;
        private ResourceSet _orthoSet;
        private ResourceLayout _texLayout;
        private Pipeline _pipeline;

        private Dictionary<string, (Texture, TextureView, ResourceSet)> _loadedImages
            = new Dictionary<string, (Texture, TextureView, ResourceSet)>();

        public SpriteRenderer(GraphicsDevice gd)
        {
            ResourceFactory factory = gd.ResourceFactory;

            _vertexBuffer = factory.CreateBuffer(new BufferDescription(1000, BufferUsage.VertexBuffer | BufferUsage.Dynamic));
            _orthoBuffer = factory.CreateBuffer(new BufferDescription(64, BufferUsage.UniformBuffer));

            _orthoLayout = factory.CreateResourceLayout(
                new ResourceLayoutDescription(
                    new ResourceLayoutElementDescription("OrthographicProjection", ResourceKind.UniformBuffer, ShaderStages.Vertex)));
            _orthoSet = factory.CreateResourceSet(new ResourceSetDescription(_orthoLayout, _orthoBuffer));

            _texLayout = factory.CreateResourceLayout(
                new ResourceLayoutDescription(
                    new ResourceLayoutElementDescription("SpriteTexture", ResourceKind.TextureReadOnly, ShaderStages.Fragment),
                    new ResourceLayoutElementDescription("SpriteSampler", ResourceKind.Sampler, ShaderStages.Fragment)));

            _pipeline = factory.CreateGraphicsPipeline(new GraphicsPipelineDescription(
                BlendStateDescription.SingleAlphaBlend,
                DepthStencilStateDescription.Disabled,
                RasterizerStateDescription.CullNone,
                PrimitiveTopology.TriangleStrip,
                new ShaderSetDescription(
                    new VertexLayoutDescription[]
                    {
                        new VertexLayoutDescription(
                            QuadVertex.VertexSize,
                            1,
                            new VertexElementDescription("Position", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2),
                            new VertexElementDescription("Size", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2),
                            new VertexElementDescription("Tint", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Byte4_Norm),
                            new VertexElementDescription("Rotation", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float1))
                    },
                    factory.CreateFromSPIRV(
                        new ShaderDescription(ShaderStages.Vertex, LoadShaderBytes("sprite.vert.spv"), "main"),
                        new ShaderDescription(ShaderStages.Fragment, LoadShaderBytes("sprite.frag.spv"), "main"),
                        GetCompilationOptions(factory))),
                new[] { _orthoLayout, _texLayout },
                gd.MainSwapchain.Framebuffer.OutputDescription));
        }

        private CompilationOptions GetCompilationOptions(ResourceFactory factory)
        {
            return new CompilationOptions(false, false, new SpecializationConstant[]
            {
                SpecializationConstant.Create(0, false)
            });
        }

        private byte[] LoadShaderBytes(string name)
        {
            return File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Assets", "Shaders", name));
        }

        public void AddSprite(Vector2 position, Vector2 size, string spriteName)
            => AddSprite(position, size, spriteName, RgbaByte.White, 0f);
        public void AddSprite(Vector2 position, Vector2 size, string spriteName, RgbaByte tint, float rotation)
        {
            _draws.Add(new SpriteInfo(spriteName, new QuadVertex(position, size, tint, rotation)));
        }

        private ResourceSet Load(GraphicsDevice gd, string spriteName)
        {
            if (!_loadedImages.TryGetValue(spriteName, out (Texture, TextureView, ResourceSet) ret))
            {
                string texPath = Path.Combine(AppContext.BaseDirectory, "Assets", spriteName);
                Texture tex = new ImageSharpTexture(texPath, false)
                    .CreateDeviceTexture(gd, gd.ResourceFactory);
                TextureView view = gd.ResourceFactory.CreateTextureView(tex);
                ResourceSet set = gd.ResourceFactory.CreateResourceSet(new ResourceSetDescription(
                    _texLayout,
                    view,
                    gd.PointSampler));
                ret = (tex, view, set);
                _loadedImages.Add(spriteName, ret);
            }

            return ret.Item3;
        }

        public void Draw(GraphicsDevice gd, CommandList cl)
        {
            if (_draws.Count == 0)
            {
                return;
            }

            float width = gd.MainSwapchain.Framebuffer.Width;
            float height = gd.MainSwapchain.Framebuffer.Height;
            gd.UpdateBuffer(
                _orthoBuffer,
                0,
                Matrix4x4.CreateOrthographicOffCenter(0, width, 0, height, 0, 1));

            EnsureBufferSize(gd, (uint)_draws.Count * QuadVertex.VertexSize);
            MappedResourceView<QuadVertex> writemap = gd.Map<QuadVertex>(_vertexBuffer, MapMode.Write);
            for (int i = 0; i < _draws.Count; i++)
            {
                writemap[i] = _draws[i].Quad;
            }
            gd.Unmap(_vertexBuffer);

            cl.SetPipeline(_pipeline);
            cl.SetVertexBuffer(0, _vertexBuffer);
            cl.SetGraphicsResourceSet(0, _orthoSet);

            for (int i = 0; i < _draws.Count;)
            {
                uint batchStart = (uint)i;
                string spriteName = _draws[i].SpriteName;
                ResourceSet rs = Load(gd, spriteName);
                cl.SetGraphicsResourceSet(1, rs);
                uint batchSize = 0;
                do
                {
                    i += 1;
                    batchSize += 1;
                }
                while (i < _draws.Count && _draws[i].SpriteName == spriteName);

                cl.Draw(4, batchSize, 0, batchStart);
            }

            _draws.Clear();
        }

        private void EnsureBufferSize(GraphicsDevice gd, uint size)
        {
            if (_vertexBuffer.SizeInBytes < size)
            {
                _vertexBuffer.Dispose();
                _vertexBuffer = gd.ResourceFactory.CreateBuffer(
                    new BufferDescription(size, BufferUsage.VertexBuffer | BufferUsage.Dynamic));
            }
        }

        private struct SpriteInfo
        {
            public SpriteInfo(string spriteName, QuadVertex quad)
            {
                SpriteName = spriteName;
                Quad = quad;
            }

            public string SpriteName { get; }
            public QuadVertex Quad { get; }
        }

        private struct QuadVertex
        {
            public const uint VertexSize = 24;

            public Vector2 Position;
            public Vector2 Size;
            public RgbaByte Tint;
            public float Rotation;

            public QuadVertex(Vector2 position, Vector2 size) : this(position, size, RgbaByte.White, 0f) { }
            public QuadVertex(Vector2 position, Vector2 size, RgbaByte tint, float rotation)
            {
                Position = position;
                Size = size;
                Tint = tint;
                Rotation = rotation;
            }
        }
    }
}
