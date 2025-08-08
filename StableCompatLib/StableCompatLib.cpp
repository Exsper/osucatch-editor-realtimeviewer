#include <windows.h>
#include <math.h>
#include <winnt.h>

typedef long double LD;

extern "C" {
    __declspec(dllexport) double __stdcall computeVelocity(
        double timingBeatLength,
        double sliderVelocityAsBeatLength,
        double difficultySliderTickRate,
        double sliderComboPointDistance
    )
    {
        double mult = (double) ((float) (-sliderVelocityAsBeatLength)) / 100.0;
        double beatLength = timingBeatLength * mult;
        LD t1 = (LD) sliderComboPointDistance * (LD) difficultySliderTickRate;
        LD t2 = (LD) 1000 / (LD) beatLength;
        return (double) (t1 * t2);
    }

    __declspec(dllexport) void __stdcall normalize(float x, float y, float *rx, float *ry)
    {
        double lengthSquared = (double) ((LD) x * x + (LD) y * y);
        float length = (float) sqrt(lengthSquared);
        LD mult = 1 / (LD) length;
        *rx = x * mult;
        *ry = y * mult;
    }

    __declspec(dllexport) double __stdcall linearInterpolation(double x, double y, double t)
    {
        LD p1 = (LD) x * (LD) t;
        LD p2 = (LD) y * (1 - (LD) t);
        return (double) (p1 + p2);
    }

    LD __stdcall tempLengthSquared(float x, float y)
    {
        return (LD) x * x + (LD) y * y;
    }

    __declspec(dllexport) float __stdcall length(float x, float y)
    {
        return (float) sqrt((double) ((LD) x * x + (LD) y * y));
    }

    __declspec(dllexport) bool isStraightLine(float ax, float ay, float bx, float by, float cx, float cy) {
        LD p1 = ((LD) bx - (LD) ax) * ((LD) cy - (LD) ay);
        LD p2 = ((LD) cx - (LD) ax) * ((LD) by - (LD) ay);
        return p1 - p2 == 0;
    }

    __declspec(dllexport) float distance(float ax, float ay, float bx, float by) {
        LD p1 = (LD) ax - (LD) bx;
        LD p2 = (LD) ay - (LD) by;
        double lengthSquared = (double) (p1 * p1 + p2 * p2);
        return (float) sqrt(lengthSquared);
    }
    
    LD __stdcall tempAtan2(double y, double x) {
        return isinf(x) && isinf(y) ? (LD) y / (LD) x : atan2l(y, x);
    }

    LD __stdcall tempCircleTAt(float x, float y, float centerx, float centery) {
        return tempAtan2(y - centery, x - centerx);
    }

    __declspec(dllexport) void __stdcall circleThroughPoints(
        float ax, float ay, float bx, float by, float cx, float cy,
        float *centerx, float *centery, float *radius, double *startAngle, double *endAngle)
    {
        constexpr double _2PI = 0x1.921fb6p2;

        LD p1 = (LD) ax * ((LD) by - (LD) cy);
        LD p2 = (LD) bx * ((LD) cy - (LD) ay);
        LD p3 = (LD) cx * ((LD) ay - (LD) by);
        float D = (float) (2 * (p1 + p2 + p3));

        float AMagSq = (float) tempLengthSquared(ax, ay);
        float BMagSq = (float) tempLengthSquared(bx, by);
        LD CMagSq = tempLengthSquared(cx, cy);

        LD p4 = (LD) AMagSq * ((LD) by - (LD) cy);
        LD p5 = (LD) BMagSq * ((LD) cy - (LD) ay);
        LD p6 = (LD) CMagSq * ((LD) ay - (LD) by);
        *centerx = (float) ((p4 + p5 + p6) / (LD) D);

        LD p7 = (LD) AMagSq * ((LD) cx - (LD) bx);
        LD p8 = (LD) BMagSq * ((LD) ax - (LD) cx);
        LD p9 = (LD) CMagSq * ((LD) bx - (LD) ax);
        *centery = (float) ((p7 + p8 + p9) / (LD) D);

        *radius = distance(*centerx, *centery, ax, ay);

        double vStartAngle = tempCircleTAt(ax, ay, *centerx, *centery);
        double midAngle = tempCircleTAt(bx, by, *centerx, *centery);
        double vEndAngle = tempCircleTAt(cx, cy, *centerx, *centery);

        LD lMidAngle = (LD) midAngle;
        while (lMidAngle < vStartAngle) lMidAngle += _2PI;
        while (vEndAngle < vStartAngle) vEndAngle += _2PI;
        if (lMidAngle > vEndAngle) vEndAngle -= _2PI;
        *startAngle = vStartAngle;
        *endAngle = vEndAngle;
    }

    __declspec(dllexport) void __stdcall circlePoint(
        float centerX, float centerY, float radius, double t,
        float *x, float *y)
    {
        *x = centerX + (float) ((long double) radius * cosl(t));
        *y = centerY + (float) ((long double) radius * sinl(t));
    }

    __declspec(dllexport) int __stdcall randomNextCalc(int value, int lowerBound, int upperBound)
    {
        constexpr float ratio = 0x1p-31f;
        return (int) ((LD) value * (LD) ratio * (LD)(upperBound - lowerBound)) + lowerBound;
    }

    __declspec(dllexport) int __stdcall timeAtLength(float length, int startTime, double velocity)
    {
        return (int) ((LD) startTime + (LD) length / (LD) velocity * 1000);
    }

    __declspec(dllexport) float __stdcall progress(double start, double end, float current)
    {
        return (float) (((LD) current - (LD) start) / ((LD) end - (LD) start));
    }

    __declspec(dllexport) double __stdcall continuousDivision(int a, int b, int c)
    {
        return (double) (LD(a) / (LD(b) / LD(c)));
    }

    __declspec(dllexport) void __stdcall bezierApproximate(
        float ax, float ay, float bx, float by, float cx, float cy,
        float *x, float *y)
    {
        *x = (float) (0.25f * ((LD) ax + 2.f * (LD) bx + (LD) cx));
        *y = (float) (0.25f * ((LD) ay + 2.f * (LD) by + (LD) cy));
    }

    __declspec(dllexport) int __stdcall flatJudge(float ax, float ay, float bx, float by, float cx, float cy)
    {
        float x = (float) ((LD) ax - 2.f * (LD) bx + (LD) cx);
        float y = (float) ((LD) ay - 2.f * (LD) by + (LD) cy);
        return tempLengthSquared(x, y) > 0.25l;
    }

    __declspec(dllexport) void __stdcall midpoint(float ax, float ay, float bx, float by, float *x, float *y)
    {
        *x = (float) (0.5f * ((LD) ax + (LD) bx));
        *y = (float) (0.5f * ((LD) ay + (LD) by));
    }

}

BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved) {
    return TRUE;
}

// #include <stdio.h>

// int main() {
//     float ax = 492, ay = 335, bx = 492.004517f, by = 334.99646f, cx = 492.009033f, cy = 334.99292f;
//     printf("%d", (int) flatJudge(ax, ay, bx, by, cx, cy));
//     return 0;
// }