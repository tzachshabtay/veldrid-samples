using SampleBase;

namespace TexturedQuad
{
    class Program
    {
        public static void Main(string[] args)
        {
            VeldridStartupWindow window = new VeldridStartupWindow("Textured Quad");
            TexturedQuad texturedCube = new TexturedQuad(window);
            window.Run();
        }
    }
}
