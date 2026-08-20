// NexusUnleashed - clean-room authored. nusl.exe — the Nexus Unleashed Server Launcher.
// A polished NATIVE control panel + resource governor for the realm server (nexus_realm.exe).
// Rendered with GDI+ (gradients, glows, rounded panels, custom sliders) — no .NET, no WebView2,
// no runtime deps, a tiny self-contained exe that ships next to nexus_realm.exe + realm.json.
//   - start / stop, live status, log tail
//   - MEMORY CAP slider (1..N GB) enforced with a Windows Job Object
//   - CPU CORES slider — process affinity mask + worker-pool size (NUSL_THREADS)
//   - live RAM (working set) + CPU% readouts
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <objidl.h>   // IStream etc. — GDI+ headers need these (trimmed by LEAN_AND_MEAN)
#include <psapi.h>
#include <gdiplus.h>
#include <dwmapi.h>   // dark title bar
#include <uxtheme.h>  // dark scrollbar theme
#include <string>
#include <vector>
#include <cstdio>
#pragma comment(lib, "gdiplus.lib")
#pragma comment(lib, "psapi.lib")
#pragma comment(lib, "dwmapi.lib")
#pragma comment(lib, "uxtheme.lib")
#ifndef DWMWA_USE_IMMERSIVE_DARK_MODE
#define DWMWA_USE_IMMERSIVE_DARK_MODE 20
#endif
using namespace Gdiplus;

// ---- palette (roadmap: black canvas, magenta + blue accents, white text) ----
static const Color cBg(255, 11, 11, 22), cPanel(255, 20, 20, 33), cPanelBrd(255, 40, 40, 74);
static const Color cMag(255, 255, 45, 155), cMagLo(255, 200, 40, 130);
static const Color cBlue(255, 74, 140, 255), cBlueLo(255, 42, 108, 226);
static const Color cWhite(255, 238, 240, 251), cMuted(255, 166, 168, 200), cDim(255, 108, 110, 140);
static const COLORREF kEditBg = RGB(0x12, 0x12, 0x1e), kEditFg = RGB(0xa6, 0xa8, 0xc8);

enum { IDC_LOG = 1, IDT_POLL = 1 };
enum Hot { H_NONE, H_START, H_STOP, H_MEM, H_CPU };

// ---- server-control state ----
static HANDLE g_proc = nullptr, g_job = nullptr; static DWORD g_pid = 0;
static std::wstring g_serverDir, g_serverExe, g_logPath, g_realmJson;
static std::wstring g_realmName = L"NexusUnleashed";
static std::wstring g_ports = L"sts 6600  ·  realm 23115  ·  world 24000";
static int g_maxCores = 8, g_maxMemGB = 16, g_memGB = 4, g_cpuCores = 8;
static ULONGLONG g_lastProcTime = 0, g_lastWall = 0;
static double g_ramMB = 0, g_cpuPct = 0;

// ---- UI state ----
static HWND g_hwnd, g_log; static HFONT g_editFont;
static Hot g_hover = H_NONE, g_press = H_NONE, g_drag = H_NONE;
static RectF rStart, rStop, rMemTrack, rCpuTrack;
static ULONG_PTR g_gdip = 0;

static std::wstring DirOfSelf() {
    wchar_t b[MAX_PATH]; GetModuleFileNameW(nullptr, b, MAX_PATH);
    std::wstring p = b; size_t s = p.find_last_of(L"\\/");
    return s == std::wstring::npos ? L"." : p.substr(0, s);
}
static std::wstring JsonVal(const std::string& j, const char* key) {
    std::string k = std::string("\"") + key + "\""; size_t p = j.find(k);
    if (p == std::string::npos) return L""; p = j.find(':', p + k.size());
    if (p == std::string::npos) return L""; size_t q = j.find('"', p);
    if (q != std::string::npos && q < j.find_first_of(",}", p)) {
        size_t e = j.find('"', q + 1); if (e == std::string::npos) return L"";
        std::string v = j.substr(q + 1, e - q - 1); return std::wstring(v.begin(), v.end());
    }
    size_t bb = j.find_first_of("0123456789", p); if (bb == std::string::npos) return L"";
    size_t e = j.find_first_not_of("0123456789", bb); std::string v = j.substr(bb, e - bb);
    return std::wstring(v.begin(), v.end());
}
static void LoadRealmInfo() {
    std::string j; FILE* f = _wfopen(g_realmJson.c_str(), L"rb");
    if (f) { char b[4096]; size_t n; while ((n = fread(b, 1, sizeof b, f)) > 0) j.append(b, n); fclose(f); }
    std::wstring nm = JsonVal(j, "RealmName");
    std::wstring s = JsonVal(j, "StsPort"), a = JsonVal(j, "AuthPort"), w = JsonVal(j, "WorldPort");
    if (!nm.empty()) g_realmName = nm;
    if (!s.empty()) g_ports = L"sts " + s + L"   ·   realm " + a + L"   ·   world " + w;
}
static bool IsRunning() {
    if (!g_proc) return false;
    if (WaitForSingleObject(g_proc, 0) == WAIT_TIMEOUT) return true;
    CloseHandle(g_proc); g_proc = nullptr; g_pid = 0; return false;
}
static void AppendLog(const std::wstring& line) {
    int n = GetWindowTextLengthW(g_log); SendMessageW(g_log, EM_SETSEL, n, n);
    std::wstring l = line + L"\r\n"; SendMessageW(g_log, EM_REPLACESEL, FALSE, (LPARAM)l.c_str());
}
static long long g_logPos = 0;
static void TailLog() {
    HANDLE h = CreateFileW(g_logPath.c_str(), GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE,
                           nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
    if (h == INVALID_HANDLE_VALUE) return;
    LARGE_INTEGER sz; GetFileSizeEx(h, &sz);
    if (sz.QuadPart < g_logPos) g_logPos = 0;
    if (sz.QuadPart > g_logPos) {
        LARGE_INTEGER pos; pos.QuadPart = g_logPos; SetFilePointerEx(h, pos, nullptr, FILE_BEGIN);
        long long avail = sz.QuadPart - g_logPos; DWORD toRead = (DWORD)(avail < (1 << 16) ? avail : (1 << 16));
        std::string buf(toRead, 0); DWORD got = 0;
        if (ReadFile(h, &buf[0], toRead, &got, nullptr) && got) {
            buf.resize(got); g_logPos += got; std::wstring w(buf.begin(), buf.end()), line;
            for (wchar_t c : w) { if (c == L'\n') { AppendLog(line); line.clear(); } else if (c != L'\r') line += c; }
            if (!line.empty()) AppendLog(line);
        }
    }
    CloseHandle(h);
}
static void StartServer() {
    if (IsRunning()) return;
    HANDLE hLog = CreateFileW(g_logPath.c_str(), GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE,
                              nullptr, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, nullptr);
    HANDLE hInherit = nullptr;
    DuplicateHandle(GetCurrentProcess(), hLog, GetCurrentProcess(), &hInherit, 0, TRUE, DUPLICATE_SAME_ACCESS);
    if (hLog != INVALID_HANDLE_VALUE) CloseHandle(hLog);
    if (g_job) { CloseHandle(g_job); g_job = nullptr; }
    g_job = CreateJobObjectW(nullptr, nullptr);
    if (g_job) {
        JOBOBJECT_EXTENDED_LIMIT_INFORMATION jeli{};
        jeli.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_JOB_MEMORY | JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;
        jeli.JobMemoryLimit = (SIZE_T)g_memGB * 1024ull * 1024ull * 1024ull;
        SetInformationJobObject(g_job, JobObjectExtendedLimitInformation, &jeli, sizeof jeli);
    }
    wchar_t th[16]; _itow(g_cpuCores, th, 10); SetEnvironmentVariableW(L"NUSL_THREADS", th);
    STARTUPINFOW si{}; si.cb = sizeof si; si.dwFlags = STARTF_USESTDHANDLES; si.hStdOutput = hInherit; si.hStdError = hInherit;
    PROCESS_INFORMATION pi{}; std::wstring cmd = L"\"" + g_serverExe + L"\"";
    std::vector<wchar_t> cb(cmd.begin(), cmd.end()); cb.push_back(0);
    BOOL ok = CreateProcessW(g_serverExe.c_str(), cb.data(), nullptr, nullptr, TRUE,
                             CREATE_NO_WINDOW | CREATE_SUSPENDED, nullptr, g_serverDir.c_str(), &si, &pi);
    if (hInherit) CloseHandle(hInherit);
    if (!ok) { AppendLog(L"[launcher] FAILED to start server (is nexus_realm.exe next to nusl.exe?)"); return; }
    if (g_job) AssignProcessToJobObject(g_job, pi.hProcess);
    DWORD_PTR mask = 0; for (int i = 0; i < g_cpuCores && i < 64; ++i) mask |= (DWORD_PTR)1 << i;
    if (mask) SetProcessAffinityMask(pi.hProcess, mask);
    ResumeThread(pi.hThread); CloseHandle(pi.hThread);
    g_proc = pi.hProcess; g_pid = pi.dwProcessId; g_logPos = 0; g_lastProcTime = g_lastWall = 0;
    wchar_t m[160]; swprintf(m, 160, L"[launcher] starting — memory cap %d GB, %d core%s.", g_memGB, g_cpuCores, g_cpuCores == 1 ? L"" : L"s");
    AppendLog(m);
}
static void StopServer() {
    if (!IsRunning()) return;
    AppendLog(L"[launcher] stopping server…"); TerminateProcess(g_proc, 0);
    WaitForSingleObject(g_proc, 3000); CloseHandle(g_proc); g_proc = nullptr; g_pid = 0;
    if (g_job) { CloseHandle(g_job); g_job = nullptr; } AppendLog(L"[launcher] server stopped.");
}
static void UpdateUsage() {
    if (!IsRunning()) { g_ramMB = 0; g_cpuPct = 0; return; }
    PROCESS_MEMORY_COUNTERS pmc{};
    if (GetProcessMemoryInfo(g_proc, &pmc, sizeof pmc)) g_ramMB = pmc.WorkingSetSize / (1024.0 * 1024.0);
    FILETIME c, e, k, u; ULONGLONG procT = 0;
    if (GetProcessTimes(g_proc, &c, &e, &k, &u))
        procT = (((ULONGLONG)k.dwHighDateTime << 32) | k.dwLowDateTime) + (((ULONGLONG)u.dwHighDateTime << 32) | u.dwLowDateTime);
    FILETIME nf; GetSystemTimeAsFileTime(&nf); ULONGLONG wall = ((ULONGLONG)nf.dwHighDateTime << 32) | nf.dwLowDateTime;
    if (g_lastWall && wall > g_lastWall) {
        double dP = (double)(procT - g_lastProcTime), dW = (double)(wall - g_lastWall);
        g_cpuPct = 100.0 * dP / (dW * (g_cpuCores > 0 ? g_cpuCores : 1));
        if (g_cpuPct < 0) g_cpuPct = 0; if (g_cpuPct > 100) g_cpuPct = 100;
    }
    g_lastProcTime = procT; g_lastWall = wall;
}

// ---------- GDI+ drawing ----------
static GraphicsPath* RoundRect(float x, float y, float w, float h, float r) {
    GraphicsPath* p = new GraphicsPath();
    p->AddArc(x, y, r, r, 180, 90); p->AddArc(x + w - r, y, r, r, 270, 90);
    p->AddArc(x + w - r, y + h - r, r, r, 0, 90); p->AddArc(x, y + h - r, r, r, 90, 90);
    p->CloseFigure(); return p;
}
static void Glow(Graphics& g, float cx, float cy, float r, Color c, int alpha) {
    GraphicsPath e; e.AddEllipse(cx - r, cy - r, 2 * r, 2 * r);
    PathGradientBrush pg(&e); pg.SetCenterColor(Color(alpha, c.GetR(), c.GetG(), c.GetB()));
    Color surround(0, c.GetR(), c.GetG(), c.GetB()); int cnt = 1; pg.SetSurroundColors(&surround, &cnt);
    g.FillEllipse(&pg, cx - r, cy - r, 2 * r, 2 * r);
}
static void FillRound(Graphics& g, float x, float y, float w, float h, float r, Color c) {
    GraphicsPath* p = RoundRect(x, y, w, h, r); SolidBrush b(c); g.FillPath(&b, p); delete p;
}
static void GradRoundV(Graphics& g, float x, float y, float w, float h, float r, Color top, Color bot) {
    GraphicsPath* p = RoundRect(x, y, w, h, r);
    LinearGradientBrush b(RectF(x, y - 1, w, h + 2), top, bot, LinearGradientModeVertical);
    g.FillPath(&b, p); delete p;
}
static void StrokeRound(Graphics& g, float x, float y, float w, float h, float r, Color c, float thick) {
    GraphicsPath* p = RoundRect(x, y, w, h, r); Pen pen(c, thick); g.DrawPath(&pen, p); delete p;
}
// y is the TOP of the text line.
static void Text(Graphics& g, const wchar_t* t, const Font& f, Color c, float x, float y, float w = 0, StringAlignment al = StringAlignmentNear) {
    SolidBrush b(c); StringFormat sf; sf.SetAlignment(al); sf.SetLineAlignment(StringAlignmentNear);
    RectF r(x, y, w > 0 ? w : 3000, 40); g.DrawString(t, -1, &f, r, &sf, &b);
}

static void DrawSlider(Graphics& g, RectF track, int val, int lo, int hi, Color accent) {
    float t = (float)(val - lo) / (float)(hi - lo <= 0 ? 1 : hi - lo);
    float cx = track.X + t * track.Width, cy = track.Y + track.Height / 2;
    // track base
    FillRound(g, track.X, cy - 3, track.Width, 6, 3, Color(255, 40, 40, 64));
    // filled
    LinearGradientBrush fb(RectF(track.X, cy - 3, track.Width, 6),
        Color(255, cBlue.GetR(), cBlue.GetG(), cBlue.GetB()), Color(255, accent.GetR(), accent.GetG(), accent.GetB()), LinearGradientModeHorizontal);
    GraphicsPath* fp = RoundRect(track.X, cy - 3, (cx - track.X < 6 ? 6 : cx - track.X), 6, 3); g.FillPath(&fb, fp); delete fp;
    // thumb glow + knob
    Glow(g, cx, cy, 16, accent, 120);
    SolidBrush kb(cWhite); g.FillEllipse(&kb, cx - 8.f, cy - 8.f, 16.f, 16.f);
    Pen kp(accent, 2.5f); g.DrawEllipse(&kp, cx - 8.f, cy - 8.f, 16.f, 16.f);
}

static void DrawMeter(Graphics& g, float x, float y, float w, float frac, Color accent, const wchar_t* label, const wchar_t* value, const Font& fLbl, const Font& fVal) {
    Text(g, label, fLbl, cMuted, x, y + 1);
    float bx = x + 52, bw = w - 52 - 132, by = y + 5, bh = 9;
    FillRound(g, bx, by, bw, bh, 4.5f, Color(255, 32, 32, 52));
    if (frac < 0) frac = 0; if (frac > 1) frac = 1;
    float fw = bw * frac;
    if (fw > 6) { GraphicsPath* p = RoundRect(bx, by, fw, bh, 4.5f);
        LinearGradientBrush b(RectF(bx, by, bw, bh), cBlue, accent, LinearGradientModeHorizontal); g.FillPath(&b, p); delete p; }
    Text(g, value, fVal, cWhite, x + w - 130, y + 1, 130, StringAlignmentFar);
}
static void CenterText(Graphics& g, const wchar_t* t, const Font& f, Color c, RectF r) {
    SolidBrush b(c); StringFormat sf; sf.SetAlignment(StringAlignmentCenter); sf.SetLineAlignment(StringAlignmentCenter);
    g.DrawString(t, -1, &f, r, &sf, &b);
}
static void DrawButton(Graphics& g, RectF r, Color accent, const wchar_t* label, const Font& f, bool enabled, bool hover, bool press) {
    if (!enabled) { FillRound(g, r.X, r.Y, r.Width, r.Height, 10, Color(255, 34, 34, 51)); StrokeRound(g, r.X, r.Y, r.Width, r.Height, 10, Color(255, 48, 48, 72), 1);
        CenterText(g, label, f, cDim, r); return; }
    Color top = accent, bot(255, accent.GetR() * 8 / 10, accent.GetG() * 8 / 10, accent.GetB() * 8 / 10);
    if (press) { Color tmp = top; top = bot; bot = tmp; }
    if (hover) Glow(g, r.X + r.Width / 2, r.Y + r.Height / 2, r.Width / 1.6f, accent, 70);
    GradRoundV(g, r.X, r.Y, r.Width, r.Height, 10, top, bot);
    StrokeRound(g, r.X, r.Y, r.Width, r.Height, 10, Color(255, min(255, accent.GetR() + 40), min(255, accent.GetG() + 40), min(255, accent.GetB() + 40)), 1);
    CenterText(g, label, f, cWhite, r);
}

static void Paint(HWND h) {
    RECT rc; GetClientRect(h, &rc); int W = rc.right, H = rc.bottom;
    HDC dc = GetDC(h);
    HDC mem = CreateCompatibleDC(dc); HBITMAP bmp = CreateCompatibleBitmap(dc, W, H); HGDIOBJ ob = SelectObject(mem, bmp);
    { Graphics g(mem); g.SetSmoothingMode(SmoothingModeAntiAlias); g.SetTextRenderingHint(TextRenderingHintClearTypeGridFit);
      SolidBrush bg(cBg); g.FillRectangle(&bg, 0, 0, W, H);
      Glow(g, W - 40.f, 20.f, 320, cMag, 46); Glow(g, 20.f, H - 20.f, 300, cBlue, 40);

      FontFamily seg(L"Segoe UI"); FontFamily mono(L"Consolas");
      Font fTitle(&seg, 27, FontStyleBold, UnitPixel), fSub(&seg, 12, FontStyleBold, UnitPixel);
      Font fBody(&seg, 15, FontStyleRegular, UnitPixel), fLbl(&seg, 13, FontStyleBold, UnitPixel);
      Font fBtn(&seg, 17, FontStyleBold, UnitPixel), fPill(&seg, 14, FontStyleBold, UnitPixel);
      Font fMono(&mono, 14, FontStyleRegular, UnitPixel), fBig(&seg, 15, FontStyleBold, UnitPixel);

      // wordmark (gradient) + subtitle
      { LinearGradientBrush tb(RectF(28, 20, 420, 40), Color(255,255,120,200), cMag, LinearGradientModeHorizontal);
        StringFormat sf; sf.SetLineAlignment(StringAlignmentCenter);
        g.DrawString(L"NEXUS UNLEASHED", -1, &fTitle, RectF(28, 22, 460, 44), &sf, &tb); }
      Text(g, L"SERVER LAUNCHER", fSub, cDim, 30, 62);

      // status pill (top-right)
      bool run = IsRunning();
      { float pw = 150, px = W - pw - 26, py = 26, ph = 34;
        FillRound(g, px, py, pw, ph, 17, Color(255, 24, 24, 40));
        StrokeRound(g, px, py, pw, ph, 17, run ? cBlue : Color(255,60,60,84), 1);
        Color dot = run ? cBlue : cDim; if (run) Glow(g, px + 22, py + ph/2, 12, cBlue, 150);
        SolidBrush db(dot); g.FillEllipse(&db, px + 15.f, py + ph/2 - 5.f, 10.f, 10.f);
        Text(g, run ? L"RUNNING" : L"STOPPED", fPill, run ? cWhite : cMuted, px + 34, py + 8, pw - 34, StringAlignmentNear); }

      // realm + ports
      Text(g, (L"Realm   " + g_realmName).c_str(), fBody, cWhite, 30, 92);
      Text(g, g_ports.c_str(), fBody, cMuted, 30, 118);

      // resource panel
      float panelX = 26, panelY = 150, panelW = W - 52, panelH = 132;
      FillRound(g, panelX, panelY, panelW, panelH, 14, cPanel);
      StrokeRound(g, panelX, panelY, panelW, panelH, 14, cPanelBrd, 1);
      wchar_t mtxt[48]; swprintf(mtxt, 48, L"%d GB", g_memGB);
      Text(g, L"MEMORY CAP", fLbl, cMuted, panelX + 22, panelY + 20);
      Text(g, mtxt, fBig, cWhite, panelX + panelW - 92, panelY + 20, 70, StringAlignmentFar);
      rMemTrack = RectF(panelX + 22, panelY + 44, panelW - 44, 6);
      DrawSlider(g, rMemTrack, g_memGB, 1, g_maxMemGB, cBlue);
      wchar_t ctxt[48]; swprintf(ctxt, 48, L"%d / %d", g_cpuCores, g_maxCores);
      Text(g, L"CPU CORES", fLbl, cMuted, panelX + 22, panelY + 76);
      Text(g, ctxt, fBig, cWhite, panelX + panelW - 92, panelY + 76, 70, StringAlignmentFar);
      rCpuTrack = RectF(panelX + 22, panelY + 100, panelW - 44, 6);
      DrawSlider(g, rCpuTrack, g_cpuCores, 1, g_maxCores, cMag);

      // buttons
      float by = 300, bw = (W - 52 - 16) / 2, bh = 50;
      rStart = RectF(26, by, bw, bh); rStop = RectF(26 + bw + 16, by, bw, bh);
      DrawButton(g, rStart, cBlue, L"START", fBtn, !run, g_hover == H_START, g_press == H_START);
      DrawButton(g, rStop, cMag, L"STOP", fBtn, run, g_hover == H_STOP, g_press == H_STOP);

      // live monitor meters (RAM + CPU) — always active, updated every tick
      wchar_t rv[64], cv[64];
      if (run) { swprintf(rv, 64, L"%.0f MB / %d GB", g_ramMB, g_memGB); swprintf(cv, 64, L"%.0f %%  · %d cores", g_cpuPct, g_cpuCores); }
      else { wcscpy(rv, L"—"); wcscpy(cv, L"—"); }
      float memFrac = run ? (float)(g_ramMB / (g_memGB * 1024.0)) : 0.f;
      float cpuFrac = run ? (float)(g_cpuPct / 100.0) : 0.f;
      DrawMeter(g, 30, 362, W - 60.f, memFrac, cBlue, L"RAM", rv, fLbl, fMono);
      DrawMeter(g, 30, 388, W - 60.f, cpuFrac, cMag, L"CPU", cv, fLbl, fMono);
    }
    BitBlt(dc, 0, 0, W, H, mem, 0, 0, SRCCOPY);
    SelectObject(mem, ob); DeleteObject(bmp); DeleteDC(mem); ReleaseDC(h, dc);
}

static Hot HitButton(int x, int y) {
    if (x >= rStart.X && x <= rStart.X + rStart.Width && y >= rStart.Y && y <= rStart.Y + rStart.Height) return H_START;
    if (x >= rStop.X && x <= rStop.X + rStop.Width && y >= rStop.Y && y <= rStop.Y + rStop.Height) return H_STOP;
    return H_NONE;
}
static Hot HitSlider(int x, int y) {
    auto inTrack = [&](RectF t) { return x >= t.X - 12 && x <= t.X + t.Width + 12 && y >= t.Y - 14 && y <= t.Y + 20; };
    if (inTrack(rMemTrack)) return H_MEM; if (inTrack(rCpuTrack)) return H_CPU; return H_NONE;
}
static void SliderSet(Hot which, int x) {
    RectF t = which == H_MEM ? rMemTrack : rCpuTrack;
    float f = (x - t.X) / t.Width; if (f < 0) f = 0; if (f > 1) f = 1;
    if (which == H_MEM) g_memGB = 1 + (int)(f * (g_maxMemGB - 1) + 0.5f);
    else g_cpuCores = 1 + (int)(f * (g_maxCores - 1) + 0.5f);
}

static LRESULT CALLBACK WndProc(HWND h, UINT m, WPARAM w, LPARAM l) {
    switch (m) {
    case WM_CREATE: {
        g_serverDir = DirOfSelf(); g_serverExe = g_serverDir + L"\\nexus_realm.exe";
        g_logPath = g_serverDir + L"\\nexus_realm.log"; g_realmJson = g_serverDir + L"\\realm.json";
        LoadRealmInfo();
        { HINSTANCE inst = (HINSTANCE)GetModuleHandleW(nullptr);
          HICON sm = (HICON)LoadImageW(inst, MAKEINTRESOURCEW(1), IMAGE_ICON, GetSystemMetrics(SM_CXSMICON), GetSystemMetrics(SM_CYSMICON), 0);
          HICON big = (HICON)LoadImageW(inst, MAKEINTRESOURCEW(1), IMAGE_ICON, 0, 0, LR_DEFAULTSIZE);
          if (sm) SendMessageW(h, WM_SETICON, ICON_SMALL, (LPARAM)sm);
          if (big) SendMessageW(h, WM_SETICON, ICON_BIG, (LPARAM)big); }   // title bar + taskbar
        SYSTEM_INFO si; GetSystemInfo(&si); g_maxCores = (int)si.dwNumberOfProcessors; if (g_maxCores < 1) g_maxCores = 1;
        MEMORYSTATUSEX ms{ sizeof ms }; GlobalMemoryStatusEx(&ms);
        int ram = (int)(ms.ullTotalPhys / (1024ull * 1024ull * 1024ull));
        g_maxMemGB = ram > 16 ? 16 : (ram < 2 ? 2 : ram);
        g_cpuCores = g_maxCores; g_memGB = g_maxMemGB >= 4 ? 4 : g_maxMemGB;
        g_editFont = CreateFontW(15, 0, 0, 0, FW_NORMAL, 0, 0, 0, DEFAULT_CHARSET, 0, 0, CLEARTYPE_QUALITY, 0, L"Consolas");
        g_log = CreateWindowExW(0, L"EDIT", L"", WS_CHILD | WS_VISIBLE | WS_VSCROLL | ES_MULTILINE | ES_READONLY | ES_AUTOVSCROLL,
                                26, 420, 100, 160, h, (HMENU)IDC_LOG, nullptr, nullptr);
        SendMessageW(g_log, WM_SETFONT, (WPARAM)g_editFont, TRUE);
        // dark mode: allow dark app theming, then give the log a dark (grey) scrollbar
        if (HMODULE ux = LoadLibraryW(L"uxtheme.dll")) {
            typedef int(WINAPI* SetPreferredAppModeFn)(int);
            if (auto spam = (SetPreferredAppModeFn)GetProcAddress(ux, MAKEINTRESOURCEA(135))) spam(1); // AllowDark
        }
        SetWindowTheme(g_log, L"DarkMode_Explorer", nullptr);
        AppendLog(L"[launcher] Nexus Unleashed Server Launcher ready.");
        wchar_t hw[128]; swprintf(hw, 128, L"[launcher] this machine: %d logical cores, %d GB cap available.", g_maxCores, g_maxMemGB); AppendLog(hw);
        SetTimer(h, IDT_POLL, 500, nullptr); return 0;
    }
    case WM_SIZE: { int W = LOWORD(l), H = HIWORD(l); MoveWindow(g_log, 26, 420, W - 52, H - 420 - 22, TRUE); return 0; }
    case WM_ERASEBKGND: return 1;
    case WM_PAINT: { PAINTSTRUCT ps; BeginPaint(h, &ps); Paint(h); EndPaint(h, &ps); return 0; }
    case WM_TIMER: UpdateUsage(); if (IsRunning()) TailLog(); InvalidateRect(h, nullptr, FALSE); return 0;
    case WM_MOUSEMOVE: {
        int x = LOWORD(l), y = HIWORD(l);
        if (g_drag != H_NONE) { SliderSet(g_drag, x); InvalidateRect(h, nullptr, FALSE); return 0; }
        Hot nh = HitButton(x, y); if (nh != g_hover) { g_hover = nh; InvalidateRect(h, nullptr, FALSE); }
        TRACKMOUSEEVENT tme{ sizeof tme, TME_LEAVE, h, 0 }; TrackMouseEvent(&tme); return 0;
    }
    case WM_MOUSELEAVE: g_hover = H_NONE; InvalidateRect(h, nullptr, FALSE); return 0;
    case WM_LBUTTONDOWN: {
        int x = LOWORD(l), y = HIWORD(l); SetCapture(h);
        Hot b = HitButton(x, y);
        bool run = IsRunning();
        if (b == H_START && !run) { g_press = H_START; }
        else if (b == H_STOP && run) { g_press = H_STOP; }
        else if (!run) { Hot s = HitSlider(x, y); if (s != H_NONE) { g_drag = s; SliderSet(s, x); } }
        InvalidateRect(h, nullptr, FALSE); return 0;
    }
    case WM_LBUTTONUP: {
        int x = LOWORD(l), y = HIWORD(l); ReleaseCapture();
        if (g_press == H_START && HitButton(x, y) == H_START) StartServer();
        else if (g_press == H_STOP && HitButton(x, y) == H_STOP) StopServer();
        g_press = H_NONE; g_drag = H_NONE; InvalidateRect(h, nullptr, FALSE); return 0;
    }
    case WM_CTLCOLORSTATIC:   // a read-only EDIT reports as static
    case WM_CTLCOLOREDIT: { HDC d = (HDC)w; SetTextColor(d, kEditFg); SetBkColor(d, kEditBg);
        static HBRUSH eb = CreateSolidBrush(kEditBg); return (LRESULT)eb; }
    case WM_CLOSE:
        if (IsRunning() && MessageBoxW(h, L"The server is still running. Stop it and exit?",
            L"Nexus Unleashed Server Launcher", MB_YESNO | MB_ICONQUESTION) != IDYES) return 0;
        StopServer(); DestroyWindow(h); return 0;
    case WM_DESTROY: KillTimer(h, IDT_POLL); PostQuitMessage(0); return 0;
    }
    return DefWindowProcW(h, m, w, l);
}

int WINAPI wWinMain(HINSTANCE hi, HINSTANCE, PWSTR, int show) {
    GdiplusStartupInput in; GdiplusStartup(&g_gdip, &in, nullptr);
    WNDCLASSW wc{}; wc.lpfnWndProc = WndProc; wc.hInstance = hi; wc.lpszClassName = L"NuslWindow";
    wc.hCursor = LoadCursor(nullptr, IDC_ARROW); wc.hbrBackground = nullptr;
    wc.hIcon = (HICON)LoadImageW(hi, MAKEINTRESOURCEW(1), IMAGE_ICON, 0, 0, LR_DEFAULTSIZE);   // taskbar / alt-tab
    RegisterClassW(&wc);
    g_hwnd = CreateWindowW(wc.lpszClassName, L"Nexus Unleashed Server Launcher",
                           (WS_OVERLAPPEDWINDOW & ~WS_MAXIMIZEBOX), CW_USEDEFAULT, CW_USEDEFAULT, 600, 660, nullptr, nullptr, hi, nullptr);
    { BOOL dark = TRUE; DwmSetWindowAttribute(g_hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, &dark, sizeof(dark)); }  // dark title bar
    ShowWindow(g_hwnd, show); UpdateWindow(g_hwnd);
    MSG msg; while (GetMessageW(&msg, nullptr, 0, 0)) { TranslateMessage(&msg); DispatchMessageW(&msg); }
    GdiplusShutdown(g_gdip); return 0;
}
