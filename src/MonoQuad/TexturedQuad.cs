using AssetPrimitives;
using SampleBase;
using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Text;
using Veldrid;
using Veldrid.SPIRV;

namespace TexturedQuad
{
    public class TexturedQuad : SampleApplication
    {
        private readonly ProcessedTexture _stoneTexData;

        private VertexPosTexCol[] _vertices1, _vertices2;
        private readonly ushort[] _indices;
        private DeviceBuffer _mvpBuffer;
        private DeviceBuffer _vertexBuffer1, _vertexBuffer2;
        private DeviceBuffer _indexBuffer;
        private CommandList _cl;
        private Texture _surfaceTexture;
        private TextureView _surfaceTextureView;
        private Pipeline _pipeline;
        private ResourceSet _worldTextureSet;
        private float _ticks;
        float x1 = 0f, y1 = 0f, x2 = 400f, y2 = 300f;
        Vector4 col1 = new Vector4(1f, 0f, 0f, 1f), col2 = new Vector4(1f, 1f, 1f, 1f);
        RgbaFloat[] colors = { new RgbaFloat(1f, 0f, 0f, 1f), new RgbaFloat(0f, 0f, 1f, 1f), new RgbaFloat(0f, 1f, 0f, 1f) };
        int currentColor;

        public TexturedQuad(ApplicationWindow window) : base(window)
        {
            _stoneTexData = LoadEmbeddedAsset<ProcessedTexture>("spnza_bricks_a_diff.binary");
            _vertices1 = GetQuadVertices(x1, y1, col1);
            _vertices2 = GetQuadVertices(x2, y2, col2);
            _indices = GetQuadIndices();
            window.KeyPressed += Window_KeyPressed;
        }

        /*private void loadTexture()
        {
            using (Stream stream = GetType().Assembly.GetManifestResourceStream(name))
            {
                if (stream == null)
                {
                    throw new InvalidOperationException("No embedded asset with the name " + name);
                }

                BinaryReader reader = new BinaryReader(stream);
                return (T)serializer.Read(reader);
            }
        }*/

        void Window_KeyPressed(KeyEvent obj)
        {
            switch (obj.Key)
            {
                case Key.Left:
                    x1 -= 100f;
                    _vertices1 = GetQuadVertices(x1, y1, col1);
                    GraphicsDevice.UpdateBuffer(_vertexBuffer1, 0, _vertices1);
                    break;
                case Key.Right:
                    x1 += 100f;
                    _vertices1 = GetQuadVertices(x1, y1, col1);
                    GraphicsDevice.UpdateBuffer(_vertexBuffer1, 0, _vertices1);
                    break;
                case Key.Down:
                    y1 -= 100f;
                    _vertices1 = GetQuadVertices(x1, y1, col1);
                    GraphicsDevice.UpdateBuffer(_vertexBuffer1, 0, _vertices1);
                    break;
                case Key.Up:
                    y1 += 100f;
                    _vertices1 = GetQuadVertices(x1, y1, col1);
                    GraphicsDevice.UpdateBuffer(_vertexBuffer1, 0, _vertices1);
                    break;
                case Key.S:
                    x2 -= 100f;
                    _vertices2 = GetQuadVertices(x2, y2, col2);
                    GraphicsDevice.UpdateBuffer(_vertexBuffer2, 0, _vertices2);
                    break;
                case Key.D:
                    x2 += 100f;
                    _vertices2 = GetQuadVertices(x2, y2, col2);
                    GraphicsDevice.UpdateBuffer(_vertexBuffer2, 0, _vertices2);
                    break;
                case Key.C:
                    currentColor = (currentColor + 1) % colors.Length;
                    break;
            }
        }

        protected unsafe override void CreateResources(ResourceFactory factory)
        {
            _mvpBuffer = factory.CreateBuffer(new BufferDescription(64, BufferUsage.UniformBuffer));

            _vertexBuffer1 = factory.CreateBuffer(new BufferDescription((uint)(VertexPosTexCol.SizeInBytes * _vertices1.Length), BufferUsage.VertexBuffer));
            GraphicsDevice.UpdateBuffer(_vertexBuffer1, 0, _vertices1);

            _vertexBuffer2 = factory.CreateBuffer(new BufferDescription((uint)(VertexPosTexCol.SizeInBytes * _vertices2.Length), BufferUsage.VertexBuffer));
            GraphicsDevice.UpdateBuffer(_vertexBuffer2, 0, _vertices2);

            _indexBuffer = factory.CreateBuffer(new BufferDescription(sizeof(ushort) * (uint)_indices.Length, BufferUsage.IndexBuffer));
            GraphicsDevice.UpdateBuffer(_indexBuffer, 0, _indices);

            _surfaceTexture = _stoneTexData.CreateDeviceTexture(GraphicsDevice, ResourceFactory, TextureUsage.Sampled);
            _surfaceTextureView = factory.CreateTextureView(_surfaceTexture);

            ShaderSetDescription shaderSet = new ShaderSetDescription(
                new[]
                {
                    new VertexLayoutDescription(
                        new VertexElementDescription("Position", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2),
                        new VertexElementDescription("TexCoords", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2),
                        new VertexElementDescription("Color", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float4))
                },
                factory.CreateFromSpirv(
                    new ShaderDescription(ShaderStages.Vertex, Encoding.UTF8.GetBytes(VertexCode), "main"),
                    new ShaderDescription(ShaderStages.Fragment, Encoding.UTF8.GetBytes(FragmentCode), "main")));

            ResourceLayout worldTextureLayout = factory.CreateResourceLayout(
                new ResourceLayoutDescription(
                    new ResourceLayoutElementDescription("MvpBuffer", ResourceKind.UniformBuffer, ShaderStages.Vertex),
                    new ResourceLayoutElementDescription("SurfaceTexture", ResourceKind.TextureReadOnly, ShaderStages.Fragment),
                    new ResourceLayoutElementDescription("SurfaceSampler", ResourceKind.Sampler, ShaderStages.Fragment)));

            _pipeline = factory.CreateGraphicsPipeline(new GraphicsPipelineDescription(
                BlendStateDescription.SingleOverrideBlend,
                DepthStencilStateDescription.Disabled,
                new RasterizerStateDescription(FaceCullMode.Back, PolygonFillMode.Solid, FrontFace.CounterClockwise, false, false),
                PrimitiveTopology.TriangleList,
                shaderSet,
                new[] { worldTextureLayout },
                MainSwapchain.Framebuffer.OutputDescription));

            _worldTextureSet = factory.CreateResourceSet(new ResourceSetDescription(
                worldTextureLayout,
                _mvpBuffer,
                _surfaceTextureView,
                GraphicsDevice.Aniso4xSampler));

            _cl = factory.CreateCommandList();
        }

        protected override void OnDeviceDestroyed()
        {
            base.OnDeviceDestroyed();
        }

        protected override void Draw(float deltaSeconds)
        {
            _ticks += deltaSeconds * 1000f;
            _cl.Begin();

            float width = 800f;
            float height = 600f;
            var projMat = Matrix4x4.CreateOrthographicOffCenter(0f, width, 0f, height, -1f, 1f);

            var viewMat = Matrix4x4.CreateLookAt(Vector3.UnitZ, Vector3.Zero, Vector3.UnitY);

            var mvp = viewMat * projMat;
            _cl.UpdateBuffer(_mvpBuffer, 0, ref mvp);

            _cl.SetFramebuffer(MainSwapchain.Framebuffer);
            _cl.ClearColorTarget(0, colors[currentColor]);
            _cl.SetPipeline(_pipeline);
            _cl.SetVertexBuffer(0, _vertexBuffer1);
            _cl.SetIndexBuffer(_indexBuffer, IndexFormat.UInt16);
            _cl.SetGraphicsResourceSet(0, _worldTextureSet);

            _cl.SetViewport(0, new Viewport(0f, 0f, base.Window.Width, base.Window.Height, -1f, 1f));

            _cl.DrawIndexed(6, 1, 0, 0, 0);

            _cl.SetVertexBuffer(0, _vertexBuffer2);
            _cl.DrawIndexed(6, 1, 0, 0, 0);

            _cl.End();
            GraphicsDevice.SubmitCommands(_cl);
            GraphicsDevice.SwapBuffers(MainSwapchain);
            //GraphicsDevice.WaitForIdle();
        }

        private static VertexPosTexCol[] GetQuadVertices(float x, float y, Vector4 col)
        {
            const float width = 200f;
            const float height = 200f;
            VertexPosTexCol[] vertices =
            {
                new VertexPosTexCol(new Vector2(-width + x, -height + y), new Vector2(0, 1), col), //bottom left
                new VertexPosTexCol(new Vector2(+width + x, -height + y), new Vector2(1, 1), col), //bottom right
                new VertexPosTexCol(new Vector2(+width + x, +height + y), new Vector2(1, 0), col), //top right
                new VertexPosTexCol(new Vector2(-width + x, +height + y), new Vector2(0, 0), col)  //top left
            };

            return vertices;
        }

        private static ushort[] GetQuadIndices()
        {
            ushort[] indices =
            {
                3,0,1, // first triangle (top left - bottom left - bottom right)
                3,1,2  // second triangle (top left - bottom right - top right)
            };

            return indices;
        }

        private const string VertexCode = @"
#version 450
layout(set = 0, binding = 0) uniform MvpBuffer
{
    mat4 Mvp;
};
layout(location = 0) in vec2 Position;
layout(location = 1) in vec2 TexCoords;
layout(location = 2) in vec4 Color;
layout(location = 0) out vec2 fsin_texCoords;
layout(location = 1) out vec4 fsin_color;
void main()
{
    vec4 pos = vec4(Position, 1., 1.);
    gl_Position = Mvp * pos;
    fsin_texCoords = TexCoords;
    fsin_color = Color;
}";

        private const string FragmentCode = @"
#version 450
layout(location = 0) in vec2 fsin_texCoords;
layout(location = 1) in vec4 fsin_color;
layout(location = 0) out vec4 fsout_color;
layout(set = 1, binding = 1) uniform texture2D SurfaceTexture;
layout(set = 1, binding = 2) uniform sampler SurfaceSampler;
void main()
{
    fsout_color =  texture(sampler2D(SurfaceTexture, SurfaceSampler), fsin_texCoords) * fsin_color;
}";
    }

    public struct VertexPosTexCol
    {
        public const uint SizeInBytes = 32;

        public float PosX;
        public float PosY;

        public float TexU;
        public float TexV;

        public float ColR;
        public float ColG;
        public float ColB;
        public float ColA;

        public VertexPosTexCol(Vector2 pos, Vector2 uv, Vector4 col)
        {
            PosX = pos.X;
            PosY = pos.Y;
            TexU = uv.X;
            TexV = uv.Y;
            ColR = col.X;
            ColG = col.Y;
            ColB = col.Z;
            ColA = col.W;
        }
    }
}
