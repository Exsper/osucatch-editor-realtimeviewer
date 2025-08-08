using System.Runtime.InteropServices;
using osuTK;

namespace osucatch_editor_realtimeviewer
{
    public partial class BeatmapConverterOsuStable
    {
        private class Segment
        {
            internal Vector2 Start;

            internal Vector2 End;

            [DllImport("StableCompatLib.dll", EntryPoint = "length")]
            internal static extern float getLength0(float x, float y);

            internal float Length
            {
                get {
                    Vector2 diff = End - Start;
                    return getLength0(diff.X, diff.Y);
                }
            }

            internal Segment(Vector2 Start, Vector2 End)
            {
                this.Start = Start;
                this.End = End;
            }
        };

    }
}
