using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Andromeda_Editor.Navigation;
using Andromeda_Editor.Editor.Settings;
using SharpDX;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Buffer = SharpDX.Direct3D11.Buffer;
using Device = SharpDX.Direct3D11.Device;
using IO = System.IO;
using Andromeda_Editor.Editor;
using System.Threading;

namespace Andromeda_Editor.Rendering
{
    // Different scenes to switch to
    public enum EditorState
    {
        PianoRoll,
        TrackList
    }

    public class EditorRenderer
    {
        int TLBarBufferLength = RenderSettings.BarBufferLength;
        int TLNoteBufferLength = RenderSettings.NoteBufferLength;

        #region structs for track list view

        // Track backgrounds
        // This is rendered every track
        [StructLayout(LayoutKind.Sequential, Pack = 16)]
        struct TLBarConstants
        {
            public float TrackTop;
            public float TrackBottom;
            public int ScreenWidth;
            public int ScreenHeight;
        }

        // Per-instance bar struct
        [StructLayout(LayoutKind.Sequential)]
        struct TLRenderBar
        {
            public float TickStart;
            public float TickLen;
            public uint BarNumber;
        }

        // Track list Notes
        [StructLayout(LayoutKind.Sequential, Pack = 16)]
        struct TLNoteConstants
        {
            public float TrackTop;
            public float TrackBottom;
            public int ScreenWidth;
            public int ScreenHeight;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct TLRenderNote
        {
            public byte Channel;
            public byte Key;
            public byte Velocity;
            public float TickStart;
            public float TickEnd;
        }

        #endregion

        // Shader Managers for the Track list view
        ShaderManager trackListBarShader;
        InputLayout trackListBarLayout;

        TLBarConstants trackListBarConstants;
        Buffer globalTrackListBarConstants;
        Buffer trackListBarBuffer;

        // Shader Managers for the Notes in the Track list view
        ShaderManager trackListNoteShader;
        InputLayout trackListNoteLayout;

        TLNoteConstants trackListNoteConstants;
        Buffer globalTrackListNoteConstants;
        Buffer trackListNoteBuffer;

        object renderLock = new object();
        int firstRenderBar = 0;
        int firstUnhitBar = 0;
        float lastBarTime = 0;

        int firstRenderNote = 0;
        int firstUnhitNote = 0;
        float lastNoteTime = 0;

        public EditorRenderer(Device device)
        {
            #region track list shader
            string trackListShaderData;

            trackListShaderData = IO.File.ReadAllText("Shaders/TrackListTrack.fx");

            trackListBarShader = new ShaderManager(
                device,
                ShaderBytecode.Compile(trackListShaderData, "VS_Bar", "vs_4_0", ShaderFlags.None, EffectFlags.None),
                ShaderBytecode.Compile(trackListShaderData, "PS", "ps_4_0", ShaderFlags.None, EffectFlags.None),
                ShaderBytecode.Compile(trackListShaderData, "GS_Bar", "gs_4_0", ShaderFlags.None, EffectFlags.None)
            );

            // Uses every struct member in TLRenderBar struct
            trackListBarLayout = new InputLayout(device, ShaderSignature.GetInputSignature(trackListBarShader.vertexShaderByteCode), new[]
            {
                new InputElement("TICK_START", 0, Format.R32_Float, 0, 0),
                new InputElement("TICK_LEN", 0, Format.R32_Float, 4, 0),
                new InputElement("BAR_NUMBER", 0, Format.R32_UInt, 8, 0),
            });

            trackListBarConstants = new TLBarConstants()
            {
                TrackTop = 1.0f,
                TrackBottom = 0.0f,
            };

            trackListBarBuffer = new Buffer(device, new BufferDescription()
            {
                BindFlags = BindFlags.VertexBuffer,
                CpuAccessFlags = CpuAccessFlags.Write,
                OptionFlags = ResourceOptionFlags.None,
                SizeInBytes = Marshal.SizeOf<TLRenderBar>() * TLBarBufferLength,
                Usage = ResourceUsage.Dynamic,
                StructureByteStride = 0
            });

            globalTrackListBarConstants = new Buffer(device, new BufferDescription()
            {
                BindFlags = BindFlags.ConstantBuffer,
                CpuAccessFlags = CpuAccessFlags.Write,
                OptionFlags = ResourceOptionFlags.None,
                SizeInBytes = Marshal.SizeOf<TLBarConstants>(),
                Usage = ResourceUsage.Dynamic,
                StructureByteStride = 0
            });
            #endregion

            #region track list notes shader

            string trackListNoteShaderData;

            trackListNoteShaderData = IO.File.ReadAllText("Shaders/TrackListNotes.fx");

            trackListNoteShader = new ShaderManager(
                device,
                ShaderBytecode.Compile(trackListNoteShaderData, "VS_Note", "vs_4_0", ShaderFlags.None, EffectFlags.None),
                ShaderBytecode.Compile(trackListNoteShaderData, "PS", "ps_4_0", ShaderFlags.None, EffectFlags.None),
                ShaderBytecode.Compile(trackListNoteShaderData, "GS_Note", "gs_4_0", ShaderFlags.None, EffectFlags.None)
            );

            trackListNoteLayout = new InputLayout(device, ShaderSignature.GetInputSignature(trackListNoteShader.vertexShaderByteCode), new[]
            {
                new InputElement("NOTE_DATA", 0, Format.R32_UInt, 0, 0),
                new InputElement("TICK_START", 0, Format.R32_Float, 4, 0),
                new InputElement("TICK_END", 0, Format.R32_Float, 8, 0)
            });

            trackListNoteConstants = new TLNoteConstants()
            {
                TrackTop = 1.0f,
                TrackBottom = 0.0f,
            };

            trackListNoteBuffer = new Buffer(device, new BufferDescription()
            {
                BindFlags = BindFlags.VertexBuffer,
                CpuAccessFlags = CpuAccessFlags.Write,
                OptionFlags = ResourceOptionFlags.None,
                SizeInBytes = Marshal.SizeOf<TLRenderNote>() * TLNoteBufferLength,
                Usage = ResourceUsage.Dynamic,
                StructureByteStride = 0
            });

            globalTrackListNoteConstants = new Buffer(device, new BufferDescription()
            {
                BindFlags = BindFlags.ConstantBuffer,
                CpuAccessFlags = CpuAccessFlags.Write,
                OptionFlags = ResourceOptionFlags.None,
                SizeInBytes = Marshal.SizeOf<TLNoteConstants>(),
                Usage = ResourceUsage.Dynamic,
                StructureByteStride = 0
            });
            #endregion

            var renderTargetDesc = new RenderTargetBlendDescription();
            renderTargetDesc.IsBlendEnabled = true;
            renderTargetDesc.SourceBlend = BlendOption.SourceAlpha;
            renderTargetDesc.DestinationBlend = BlendOption.InverseSourceAlpha;
            renderTargetDesc.BlendOperation = BlendOperation.Add;
            renderTargetDesc.SourceAlphaBlend = BlendOption.One;
            renderTargetDesc.DestinationAlphaBlend = BlendOption.One;
            renderTargetDesc.AlphaBlendOperation = BlendOperation.Add;
            renderTargetDesc.RenderTargetWriteMask = ColorWriteMaskFlags.All;

            BlendStateDescription desc = new BlendStateDescription();
            desc.AlphaToCoverageEnable = false;
            desc.IndependentBlendEnable = false;
            desc.RenderTarget[0] = renderTargetDesc;

            var blendStateEnabled = new BlendState(device, desc);
            device.ImmediateContext.OutputMerger.SetBlendState(blendStateEnabled);

            RasterizerStateDescription renderStateDesc = new RasterizerStateDescription
            {
                CullMode = CullMode.None,
                DepthBias = 0,
                DepthBiasClamp = 0,
                FillMode = FillMode.Solid,
                IsAntialiasedLineEnabled = false,
                IsDepthClipEnabled = false,
                IsFrontCounterClockwise = false,
                IsMultisampleEnabled = true,
                IsScissorEnabled = false,
                SlopeScaledDepthBias = 0
            };
            var rasterStateSolid = new RasterizerState(device, renderStateDesc);
            device.ImmediateContext.Rasterizer.State = rasterStateSolid;
        }

        // Updates shader with new struct
        void SetTrackListShaderConstants(DeviceContext context, TLBarConstants constants)
        {
            DataStream data;
            context.MapSubresource(globalTrackListBarConstants, 0, MapMode.WriteDiscard, SharpDX.Direct3D11.MapFlags.None, out data);
            data.Write(constants);
            context.UnmapSubresource(globalTrackListBarConstants, 0);
            context.VertexShader.SetConstantBuffer(0, globalTrackListBarConstants);
            context.GeometryShader.SetConstantBuffer(0, globalTrackListBarConstants);
            data.Dispose();
        }

        void SetTrackListNoteShaderConstants(DeviceContext context, TLNoteConstants constants)
        {
            DataStream data;
            context.MapSubresource(globalTrackListNoteConstants, 0, MapMode.WriteDiscard, SharpDX.Direct3D11.MapFlags.None, out data);
            data.Write(constants);
            context.UnmapSubresource(globalTrackListNoteConstants, 0);
            context.VertexShader.SetConstantBuffer(0, globalTrackListNoteConstants);
            context.GeometryShader.SetConstantBuffer(0, globalTrackListNoteConstants);
            data.Dispose();
        }

        public void Render(Device device, RenderTargetView target, DrawEventArgs args)
        {
            // Clear color
            var context = device.ImmediateContext;

            // Switch shader to Track List
            context.InputAssembler.InputLayout = trackListBarLayout;
            trackListBarShader.SetShaders(context);
            trackListBarConstants.ScreenWidth = (int)args.RenderSize.Width;
            trackListBarConstants.ScreenHeight = (int)args.RenderSize.Height;
            SetTrackListShaderConstants(context, trackListBarConstants);

            context.ClearRenderTargetView(target, new Color4(0.0f, 0.0f, 0.0f, 1.0f));

            #region bars
            lock (renderLock)
            {
                float realTickPos = (float)GlobalNavigation.TickPosition;
                float lastTickPos = lastBarTime;
                int lastHitBar = firstUnhitBar - 1;
                int barsRendered = 0;
                unsafe
                {
                    TLRenderBar* rb = stackalloc TLRenderBar[TLBarBufferLength * 2];

                    int barOff = firstRenderBar;
                    int bid = 0;

                    if (lastTickPos > realTickPos)
                    {
                        for (barOff = 0; barOff < TrackBars.cachedBars.Length &&
                            TrackBars.cachedBars[barOff].Tick + TrackBars.cachedBars[barOff].Length <= realTickPos; barOff++)
                        {
                            if (TrackBars.cachedBars[barOff].Tick + TrackBars.cachedBars[barOff].Length > realTickPos) break;
                        }
                        firstRenderBar = barOff;
                    }
                    else if (lastTickPos < realTickPos)
                    {
                        for (; barOff < TrackBars.cachedBars.Length &&
                            TrackBars.cachedBars[barOff].Tick + TrackBars.cachedBars[barOff].Length <= realTickPos; barOff++)
                        {
                            if (TrackBars.cachedBars[barOff].Tick + TrackBars.cachedBars[barOff].Length > realTickPos) break;
                        }
                        firstRenderBar = barOff;
                    }

                    double realZoom = TrackListNavigation.XZoom;

                    double trackScale = 1.0 / TrackListNavigation.YZoom;
                    double trackTop;
                    double trackBottom;

                    double trackOffset = TrackListNavigation.TrackOffset * trackScale;

                    RenderLoop(Enumerable.Range(0, 64), track =>
                    {
                        trackTop = 1.0 - (trackScale * track) - trackOffset;
                        trackBottom = trackTop - (trackScale - 1.0 / args.RenderSize.Height);
                        if (trackTop < 0) return; // it's out of the screen!

                        while (barOff < TrackBars.cachedBars.Length && TrackBars.cachedBars[barOff].Tick < realTickPos + realZoom)
                        {
                            var bar = TrackBars.cachedBars[barOff++];
                            if (bar.Tick + bar.Length < realTickPos)
                            {
                                lastHitBar = barOff - 1;
                                continue;
                            }
                            if (bar.Tick < realTickPos)
                            {
                                lastHitBar = barOff - 1;
                            }
                            barsRendered++;
                            rb[bid++] = new TLRenderBar()
                            {
                                TickStart = ((float)(bar.Tick - realTickPos) / (float)realZoom),
                                TickLen = (float)bar.Length / (float)realZoom,
                                BarNumber = (uint)bar.BarNumber
                            };
                            if (bid == TLBarBufferLength)
                            {
                                FlushTrackListBuffer(context, (float)trackTop, (float)trackBottom, (IntPtr)rb, bid);
                                bid = 0;
                            }
                        }
                        FlushTrackListBuffer(context, (float)trackTop, (float)trackBottom, (IntPtr)rb, bid);
                    });
                }
                if (barsRendered == 0) lastHitBar = firstRenderBar - 1;
                firstUnhitBar = lastHitBar + 1;

                lastBarTime = realTickPos;
            }
            #endregion

            #region notes

            #endregion
        }

        void RenderLoop<T>(IEnumerable<T> iterator, Action<T> render)
        {
            if (RenderSettings.MultiThreadedRendering)
            {
                Parallel.ForEach(iterator, new ParallelOptions() { MaxDegreeOfParallelism = RenderSettings.RenderThreads }, render);
            }
            else
            {
                foreach (T item in iterator) render(item);
            }
        }

        unsafe void FlushTrackListBuffer(DeviceContext context, float top, float bottom, IntPtr bars, int count)
        {
            if (count == 0) return;
            if (!RenderSettings.MultiThreadedRendering) Monitor.Enter(context);
            DataStream data;
            context.MapSubresource(trackListBarBuffer, 0, MapMode.WriteDiscard, SharpDX.Direct3D11.MapFlags.None, out data);
            data.Position = 0;
            data.WriteRange(bars, count * sizeof(TLRenderBar));
            context.UnmapSubresource(trackListBarBuffer, 0);
            context.InputAssembler.PrimitiveTopology = PrimitiveTopology.PointList;
            context.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(trackListBarBuffer, sizeof(TLRenderBar), 0));
            trackListBarConstants.TrackTop = top;
            trackListBarConstants.TrackBottom = bottom;
            SetTrackListShaderConstants(context, trackListBarConstants);
            context.Draw(count, 0);
            data.Dispose();
            if (!RenderSettings.MultiThreadedRendering) Monitor.Exit(context);
        }

        public void Dispose()
        {

        }
    }
}
