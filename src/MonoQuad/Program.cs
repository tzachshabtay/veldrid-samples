using System;
using SampleBase;

namespace MonoQuad
{
    class MainClass
    {
        public static void Main(string[] args)
        {
            VeldridStartupWindow window = new VeldridStartupWindow("Mono Quad");
            TexturedQuad.TexturedQuad texturedCube = new TexturedQuad.TexturedQuad(window);
            window.Run();
        }
    }
}
