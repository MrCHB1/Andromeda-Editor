float TrackTop;
float TrackBottom;
int ScreenWidth;
int ScreenHeight;

struct BAR
{
	float start : TICK_START;
	float length : TICK_LEN;
	dword bar_number : BAR_NUMBER;
};

struct VS_IN
{
	float4 pos : POSITION;
	float4 col : COLOR;
};

struct PS_IN
{
	float4 pos : SV_POSITION;
	float2 uv : TEXCOORD;
	float4 col : COLOR;
};

BAR VS_Bar(BAR input)
{
	return input;
}

[maxvertexcount(6)]
void GS_Bar(point BAR input[1], inout TriangleStream<PS_IN> OutputStream)
{
	BAR b = input[0];
	PS_IN v = (PS_IN)0;

	float4 barColor_Even = float4(35, 34, 44, 255) / 255;
	float4 barColor_Odd = float4(14, 14, 18, 255) / 255;

	float4 cl = barColor_Even;

	if (b.bar_number % 8 >= 4) cl = barColor_Odd;

	v.col = cl;
	v.pos = float4(b.start, TrackTop, 0, 1);
	v.pos.xy = v.pos.xy * 2 - 1;
	v.uv = float2(0.0, (TrackTop - TrackBottom) * (float)ScreenHeight);
	OutputStream.Append(v);
	v.pos = float4(b.start, TrackBottom, 0, 1);
	v.pos.xy = v.pos.xy * 2 - 1;
	v.uv = float2(0.0, 0.0);
	OutputStream.Append(v);
	v.pos = float4(b.start + b.length, TrackBottom, 0, 1);
	v.pos.xy = v.pos.xy * 2 - 1;
	v.uv = float2(b.length * (float)ScreenWidth, 0.0);
	OutputStream.Append(v);
	OutputStream.RestartStrip();

	v.pos = float4(b.start + b.length, TrackBottom, 0, 1);
	v.pos.xy = v.pos.xy * 2 - 1;
	v.uv = float2(b.length * (float)ScreenWidth, 0.0);
	OutputStream.Append(v);
	v.pos = float4(b.start + b.length, TrackTop, 0, 1);
	v.pos.xy = v.pos.xy * 2 - 1;
	v.uv = float2(b.length * (float)ScreenWidth, (TrackTop - TrackBottom) * (float)ScreenHeight);
	OutputStream.Append(v);
	v.pos = float4(b.start, TrackTop, 0, 1);
	v.pos.xy = v.pos.xy * 2 - 1;
	v.uv = float2(0.0, (TrackTop - TrackBottom) * (float)ScreenHeight);
	OutputStream.Append(v);
	OutputStream.RestartStrip();
}

float4 PS(PS_IN input) : SV_Target
{
	PS_IN i = input;
	if (round(i.uv.x - 0.49) < 1) i.col.xyz = float3(25,24,34)/255;
	return i.col;
}