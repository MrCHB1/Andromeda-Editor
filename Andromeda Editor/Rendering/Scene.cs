using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Andromeda_Editor.Rendering
{
    class Scene : IDirect3D
    {
        EditorRenderer renderer;

        public virtual D3D11 Renderer
        {
            get { return context; }
            set
            {
                if (Renderer != null)
                {
                    Renderer.Rendering -= ContextRendering;
                    Detach();
                }
                context = value;
                if (Renderer != null)
                {
                    Renderer.Rendering += ContextRendering;
                    Attach();
                }
            }
        }

        D3D11 context;

        public Scene() { }

        void ContextRendering(object ctx, DrawEventArgs args)
        {
            RenderScene(args);
        }

        protected void Attach()
        {
            if (Renderer == null) return;

            renderer = new EditorRenderer(Renderer.Device);
        }

        protected void Detach()
        {
            renderer.Dispose();
        }

        public void RenderScene(DrawEventArgs args)
        {
            renderer.Render(Renderer.Device, Renderer.RenderTargetView, args);
        }


        void IDirect3D.Reset(DrawEventArgs args)
        {
            if (Renderer != null) Renderer.Reset(args);
        }

        void IDirect3D.Render(DrawEventArgs args)
        {
            if (Renderer != null) Renderer.Render(args);
        }
    }
}
