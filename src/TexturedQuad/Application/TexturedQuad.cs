using AssetPrimitives;
using SampleBase;
using System.Numerics;
using System.Text;
using Veldrid;
using Veldrid.SPIRV;

namespace TexturedQuad
{
    public class TexturedQuad : SampleApplication
    {
        private readonly ProcessedTexture _stoneTexData;

        private VertexPositionTexture[] _vertices1, _vertices2;
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
        float x1 = 1.1f, y1 = 0.1f, x2 = -1.1f, y2 = 0.1f;

        public TexturedQuad(ApplicationWindow window) : base(window)
        {
            _stoneTexData = LoadEmbeddedAsset<ProcessedTexture>("spnza_bricks_a_diff.binary");
            _vertices1 = GetQuadVertices(x1, y1);
            _vertices2 = GetQuadVertices(x2, y2);
            _indices = GetQuadIndices();
            window.KeyPressed += Window_KeyPressed;
        }

        void Window_KeyPressed(KeyEvent obj)
        {
            switch (obj.Key)
            {
                case Key.Left:
                    x1 -= 0.1f;
                    _vertices1 = GetQuadVertices(x1, y1);
                    GraphicsDevice.UpdateBuffer(_vertexBuffer1, 0, _vertices1);
                    break;
                case Key.Right:
                    x1 += 0.1f;
                    _vertices1 = GetQuadVertices(x1, y1);
                    GraphicsDevice.UpdateBuffer(_vertexBuffer1, 0, _vertices1);
                    break;
                case Key.S:
                    x2 -= 0.1f;
                    _vertices2 = GetQuadVertices(x2, y2);
                    GraphicsDevice.UpdateBuffer(_vertexBuffer2, 0, _vertices2);
                    break;
                case Key.D:
                    x2 += 0.1f;
                    _vertices2 = GetQuadVertices(x2, y2);
                    GraphicsDevice.UpdateBuffer(_vertexBuffer2, 0, _vertices2);
                    break;
            }
        }

        protected unsafe override void CreateResources(ResourceFactory factory)
        {
            _mvpBuffer = factory.CreateBuffer(new BufferDescription(64, BufferUsage.UniformBuffer));

            _vertexBuffer1 = factory.CreateBuffer(new BufferDescription((uint)(VertexPositionTexture.SizeInBytes * _vertices1.Length), BufferUsage.VertexBuffer));
            GraphicsDevice.UpdateBuffer(_vertexBuffer1, 0, _vertices1);

            _vertexBuffer2 = factory.CreateBuffer(new BufferDescription((uint)(VertexPositionTexture.SizeInBytes * _vertices2.Length), BufferUsage.VertexBuffer));
            GraphicsDevice.UpdateBuffer(_vertexBuffer2, 0, _vertices2);

            _indexBuffer = factory.CreateBuffer(new BufferDescription(sizeof(ushort) * (uint)_indices.Length, BufferUsage.IndexBuffer));
            GraphicsDevice.UpdateBuffer(_indexBuffer, 0, _indices);

            _surfaceTexture = _stoneTexData.CreateDeviceTexture(GraphicsDevice, ResourceFactory, TextureUsage.Sampled);
            _surfaceTextureView = factory.CreateTextureView(_surfaceTexture);

            ShaderSetDescription shaderSet = new ShaderSetDescription(
                new[]
                {
                    new VertexLayoutDescription(
                        new VertexElementDescription("Position", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3),
                        new VertexElementDescription("TexCoords", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float2))
                },
                factory.CreateFromSpirv(
                    new ShaderDescription(ShaderStages.Vertex, Encoding.UTF8.GetBytes(VertexCode), "main"),
                    new ShaderDescription(ShaderStages.Fragment, Encoding.UTF8.GetBytes(FragmentCode), "main")));

            ResourceLayout worldTextureLayout = factory.CreateResourceLayout(
                new ResourceLayoutDescription(
                    new ResourceLayoutElementDescription("Mvp", ResourceKind.UniformBuffer, ShaderStages.Vertex),
                    new ResourceLayoutElementDescription("SurfaceTexture", ResourceKind.TextureReadOnly, ShaderStages.Fragment),
                    new ResourceLayoutElementDescription("SurfaceSampler", ResourceKind.Sampler, ShaderStages.Fragment)));

            _pipeline = factory.CreateGraphicsPipeline(new GraphicsPipelineDescription(
                BlendStateDescription.SingleOverrideBlend,
                DepthStencilStateDescription.DepthOnlyLessEqual,
                RasterizerStateDescription.Default,
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

            var projMat = Matrix4x4.CreatePerspectiveFieldOfView(
                1.0f,
                (float)Window.Width / Window.Height,
                0.5f,
                100f);

            var viewMat = Matrix4x4.CreateLookAt(Vector3.UnitZ * 2.5f, Vector3.Zero, Vector3.UnitY);

            Matrix4x4 rotation =
                Matrix4x4.CreateFromAxisAngle(Vector3.UnitY, (30000f / 1000f))
                * Matrix4x4.CreateFromAxisAngle(Vector3.UnitX, (30000f / 3000f));

            var mvp = rotation * viewMat * projMat;
            _cl.UpdateBuffer(_mvpBuffer, 0, ref mvp);

            _cl.SetFramebuffer(MainSwapchain.Framebuffer);
            _cl.ClearColorTarget(0, RgbaFloat.Black);
            _cl.ClearDepthStencil(1f);
            _cl.SetPipeline(_pipeline);
            _cl.SetVertexBuffer(0, _vertexBuffer1);
            _cl.SetIndexBuffer(_indexBuffer, IndexFormat.UInt16);
            _cl.SetGraphicsResourceSet(0, _worldTextureSet);
            _cl.DrawIndexed(6, 1, 0, 0, 0);

            _cl.SetVertexBuffer(0, _vertexBuffer2);
            _cl.DrawIndexed(6, 1, 0, 0, 0);

            _cl.End();
            GraphicsDevice.SubmitCommands(_cl);
            GraphicsDevice.SwapBuffers(MainSwapchain);
            //GraphicsDevice.WaitForIdle();
        }

        private static VertexPositionTexture[] GetQuadVertices(float x, float y)
        {
            VertexPositionTexture[] vertices =
            {
                new VertexPositionTexture(new Vector3(-0.5f + x, -0.5f + y, 0f), new Vector2(0, 1)), //bottom left
                new VertexPositionTexture(new Vector3(+0.5f + x, -0.5f + y, 0f), new Vector2(1, 1)), //bottom right
                new VertexPositionTexture(new Vector3(+0.5f + x, +0.5f + y, 0f), new Vector2(1, 0)), //top right
                new VertexPositionTexture(new Vector3(-0.5f + x, +0.5f + y, 0f), new Vector2(0, 0))  //top left
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
layout(location = 0) in vec3 Position;
layout(location = 1) in vec2 TexCoords;
layout(location = 0) out vec2 fsin_texCoords;
void main()
{
    vec4 pos = Mvp * vec4(Position, 1);
    gl_Position = pos;
    fsin_texCoords = TexCoords;
}";

        private const string FragmentCode = @"
#version 450
layout(location = 0) in vec2 fsin_texCoords;
layout(location = 0) out vec4 fsout_color;
layout(set = 1, binding = 1) uniform texture2D SurfaceTexture;
layout(set = 1, binding = 2) uniform sampler SurfaceSampler;
void main()
{
    fsout_color =  texture(sampler2D(SurfaceTexture, SurfaceSampler), fsin_texCoords);
}";
    }

    public struct VertexPositionTexture
    {
        public const uint SizeInBytes = 20;

        public float PosX;
        public float PosY;
        public float PosZ;

        public float TexU;
        public float TexV;

        public VertexPositionTexture(Vector3 pos, Vector2 uv)
        {
            PosX = pos.X;
            PosY = pos.Y;
            PosZ = pos.Z;
            TexU = uv.X;
            TexV = uv.Y;
        }
    }
}
