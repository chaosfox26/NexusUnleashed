// NexusUnleashed - clean-room authored. nusl.exe — the Nexus Unleashed Server Launcher.
// A tiny native Win32 control panel + resource governor for the realm server (nexus_realm.exe):
//   - start / stop, live status, log tail
//   - MEMORY CAP slider (1..N GB) enforced with a Windows Job Object — the server physically
//     cannot exceed the dialed limit
//   - CPU CORES slider — a process affinity mask spreads (or restricts) the load across cores,
//     and the chosen thread count is handed to the server (NUSL_THREADS) for its worker pool
//   - live RAM (working set) + CPU% readouts
// No .NET, no runtime deps. Ships next to nexus_realm.exe + realm.json.
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <commctrl.h>
#include <psapi.h>
#include <string>
#include <vector>
#include <cstdio>
#pragma comment(lib, "comctl32.lib")
#pragma comment(lib, "psapi.lib")

// ---- palette (matches the roadmap: black canvas, magenta + blue accents, white text) ----
static const COLORREF kBg      = RGB(0x0b, 0x0b, 0x16);
static const COLORREF kPanel   = RGB(0x14, 0x14, 0x1f);
static const COLORREF kMagenta = RGB(0xff, 0x2d, 0x9b);
static const COLORREF kBlue    = RGB(0x4a, 0x8c, 0xff);
static const COLORREF kWhite   = RGB(0xee, 0xf0, 0xfb);
static const COLORREF kMuted   = RGB(0xa6, 0xa8, 0xc8);
static const COLORREF kDim     = RGB(0x6c, 0x6e, 0x8c);

enum { IDC_START = 1001, IDC_STOP, IDC_STATUS, IDC_TITLE, IDC_SUB, IDC_REALM, IDC_PORTS,
       IDC_MEMSLIDER, IDC_MEMLBL, IDC_CPUSLIDER, IDC_CPULBL, IDC_USAGE, IDC_LOG, IDT_POLL = 1 };

// ---- state ----
static HANDLE g_proc = nullptr, g_job = nullptr;
static DWORD  g_pid  = 0;
static std::wstring g_serverDir, g_serverExe, g_logPath, g_realmJson;
static std::wstring g_realmName = L"NexusUnleashed";
static std::wstring g_ports = L"sts 6600 · realm 23115 · world 24000";
static HWND g_start, g_stop, g_status, g_realmLbl, g_portsLbl, g_memSlider, g_memLbl, g_cpuSlider, g_cpuLbl, g_usage, g_log;
static HFONT g_fTitle, g_fSub, g_fBody, g_fBtn, g_fMono, g_fStatus, g_fSmall;
static HBRUSH g_bgBrush, g_panelBrush;
static long long g_logPos = 0;
static int g_maxCores = 8, g_maxMemGB = 16;
static int g_memGB = 4, g_cpuCores = 8;          // current slider selections
// CPU% tracking
static ULONGLONG g_lastProcTime = 0, g_lastWall = 0;

static std::wstring DirOfSelf() {
    wchar_t buf[MAX_PATH]; GetModuleFileNameW(nullptr, buf, MAX_PATH);
    std::wstring p = buf; size_t s = p.find_last_of(L"\\/");
    return s == std::wstring::npos ? L"." : p.substr(0, s);
}

static std::wstring JsonStr(const std::string& j, const char* key) {
    std::string k = std::string("\"") + key + "\"";
    size_t p = j.find(k); if (p == std::string::npos) return L"";
    p = j.find(':', p + k.size()); if (p == std::string::npos) return L"";
    size_t q = j.find('"', p);
    if (q != std::string::npos && q < j.find_first_of(",}", p)) {
        size_t e = j.find('"', q + 1); if (e == std::string::npos) return L"";
        std::string v = j.substr(q + 1, e - q - 1); return std::wstring(v.begin(), v.end());
    }
    size_t b = j.find_first_of("0123456789", p);
    if (b == std::string::npos) return L"";
    size_t e = j.find_first_not_of("0123456789", b);
    std::string v = j.substr(b, e - b); return std::wstring(v.begin(), v.end());
}

static void LoadRealmInfo() {
    std::string j;
    FILE* f = _wfopen(g_realmJson.c_str(), L"rb");
    if (f) { char buf[4096]; size_t n; while ((n = fread(buf, 1, sizeof buf, f)) > 0) j.append(buf, n); fclose(f); }
    std::wstring name = JsonStr(j, "RealmName");
    std::wstring sts = JsonStr(j, "StsPort"), auth = JsonStr(j, "AuthPort"), world = JsonStr(j, "WorldPort");
    if (!name.empty()) g_realmName = name;
    if (!sts.empty()) g_ports = L"sts " + sts + L" · realm " + auth + L" · world " + world;
}

static bool IsRunning() {
    if (!g_proc) return false;
    if (WaitForSingleObject(g_proc, 0) == WAIT_TIMEOUT) return true;
    CloseHandle(g_proc); g_proc = nullptr; g_pid = 0; return false;
}

static void AppendLog(const std::wstring& line) {
    int len = GetWindowTextLengthW(g_log);
    SendMessageW(g_log, EM_SETSEL, len, len);
    std::wstring l = line + L"\r\n";
    SendMessageW(g_log, EM_REPLACESEL, FALSE, (LPARAM)l.c_str());
}

static void TailLog() {
    HANDLE h = CreateFileW(g_logPath.c_str(), GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE,
                           nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
    if (h == INVALID_HANDLE_VALUE) return;
    LARGE_INTEGER sz; GetFileSizeEx(h, &sz);
    if (sz.QuadPart < g_logPos) g_logPos = 0;
    if (sz.QuadPart > g_logPos) {
        LARGE_INTEGER pos; pos.QuadPart = g_logPos; SetFilePointerEx(h, pos, nullptr, FILE_BEGIN);
        long long avail = sz.QuadPart - g_logPos;
        DWORD toRead = (DWORD)(avail < (1 << 16) ? avail : (1 << 16));
        std::string buf(toRead, 0); DWORD got = 0;
        if (ReadFile(h, &buf[0], toRead, &got, nullptr) && got) {
            buf.resize(got); g_logPos += got;
            std::wstring w(buf.begin(), buf.end()), line;
            for (wchar_t c : w) { if (c == L'\n') { AppendLog(line); line.clear(); } else if (c != L'\r') line += c; }
            if (!line.empty()) AppendLog(line);
        }
    }
    CloseHandle(h);
}

static void StartServer() {
    if (IsRunning()) return;
    g_memGB = (int)SendMessageW(g_memSlider, TBM_GETPOS, 0, 0);
    g_cpuCores = (int)SendMessageW(g_cpuSlider, TBM_GETPOS, 0, 0);

    // Fresh log.
    HANDLE hLog = CreateFileW(g_logPath.c_str(), GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE,
                              nullptr, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, nullptr);
    HANDLE hInherit = nullptr;
    DuplicateHandle(GetCurrentProcess(), hLog, GetCurrentProcess(), &hInherit, 0, TRUE, DUPLICATE_SAME_ACCESS);
    if (hLog != INVALID_HANDLE_VALUE) CloseHandle(hLog);

    // Job object with a hard memory cap. KILL_ON_JOB_CLOSE ties the server's life to the launcher.
    if (g_job) { CloseHandle(g_job); g_job = nullptr; }
    g_job = CreateJobObjectW(nullptr, nullptr);
    if (g_job) {
        JOBOBJECT_EXTENDED_LIMIT_INFORMATION jeli{};
        jeli.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_JOB_MEMORY | JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;
        jeli.JobMemoryLimit = (SIZE_T)g_memGB * 1024ull * 1024ull * 1024ull;
        SetInformationJobObject(g_job, JobObjectExtendedLimitInformation, &jeli, sizeof jeli);
    }

    // Hand the server its worker-thread count (it reads NUSL_THREADS at boot).
    wchar_t th[16]; _itow(g_cpuCores, th, 10);
    SetEnvironmentVariableW(L"NUSL_THREADS", th);

    STARTUPINFOW si{}; si.cb = sizeof si;
    si.dwFlags = STARTF_USESTDHANDLES; si.hStdOutput = hInherit; si.hStdError = hInherit;
    PROCESS_INFORMATION pi{};
    std::wstring cmd = L"\"" + g_serverExe + L"\"";
    std::vector<wchar_t> cmdBuf(cmd.begin(), cmd.end()); cmdBuf.push_back(0);
    BOOL ok = CreateProcessW(g_serverExe.c_str(), cmdBuf.data(), nullptr, nullptr, TRUE,
                             CREATE_NO_WINDOW | CREATE_SUSPENDED, nullptr, g_serverDir.c_str(), &si, &pi);
    if (hInherit) CloseHandle(hInherit);
    if (!ok) { AppendLog(L"[launcher] FAILED to start server (is nexus_realm.exe next to nusl.exe?)"); return; }

    if (g_job) AssignProcessToJobObject(g_job, pi.hProcess);
    // Affinity: allow the first g_cpuCores logical processors.
    DWORD_PTR mask = 0; for (int i = 0; i < g_cpuCores && i < 64; ++i) mask |= (DWORD_PTR)1 << i;
    if (mask) SetProcessAffinityMask(pi.hProcess, mask);
    ResumeThread(pi.hThread);
    CloseHandle(pi.hThread);
    g_proc = pi.hProcess; g_pid = pi.dwProcessId; g_logPos = 0;
    g_lastProcTime = 0; g_lastWall = 0;
    wchar_t msg[160];
    swprintf(msg, 160, L"[launcher] starting — memory cap %d GB, %d core%s.", g_memGB, g_cpuCores, g_cpuCores == 1 ? L"" : L"s");
    AppendLog(msg);
}

static void StopServer() {
    if (!IsRunning()) return;
    AppendLog(L"[launcher] stopping server…");
    TerminateProcess(g_proc, 0);
    WaitForSingleObject(g_proc, 3000);
    CloseHandle(g_proc); g_proc = nullptr; g_pid = 0;
    if (g_job) { CloseHandle(g_job); g_job = nullptr; }
    AppendLog(L"[launcher] server stopped.");
}

static void UpdateUsage() {
    if (!IsRunning()) { SetWindowTextW(g_usage, L"RAM  —     CPU  —"); return; }
    PROCESS_MEMORY_COUNTERS pmc{};
    double memMB = 0;
    if (GetProcessMemoryInfo(g_proc, &pmc, sizeof pmc)) memMB = pmc.WorkingSetSize / (1024.0 * 1024.0);
    // CPU%
    FILETIME c, e, k, u; ULONGLONG procT = 0; double cpu = 0;
    if (GetProcessTimes(g_proc, &c, &e, &k, &u)) {
        procT = (((ULONGLONG)k.dwHighDateTime << 32) | k.dwLowDateTime) +
                (((ULONGLONG)u.dwHighDateTime << 32) | u.dwLowDateTime);
    }
    FILETIME nowFt; GetSystemTimeAsFileTime(&nowFt);
    ULONGLONG wall = ((ULONGLONG)nowFt.dwHighDateTime << 32) | nowFt.dwLowDateTime;
    if (g_lastWall && wall > g_lastWall) {
        double dProc = (double)(procT - g_lastProcTime);
        double dWall = (double)(wall - g_lastWall);
        cpu = 100.0 * dProc / (dWall * (g_cpuCores > 0 ? g_cpuCores : 1));
        if (cpu < 0) cpu = 0; if (cpu > 100) cpu = 100;
    }
    g_lastProcTime = procT; g_lastWall = wall;
    wchar_t s[160];
    swprintf(s, 160, L"RAM  %.0f MB / %d GB      CPU  %.0f%%  across %d core%s",
             memMB, g_memGB, cpu, g_cpuCores, g_cpuCores == 1 ? L"" : L"s");
    SetWindowTextW(g_usage, s);
}

static void RefreshUI() {
    bool run = IsRunning();
    SetWindowTextW(g_status, run ? L"●  RUNNING" : L"●  STOPPED");
    EnableWindow(g_start, !run); EnableWindow(g_stop, run);
    EnableWindow(g_memSlider, !run); EnableWindow(g_cpuSlider, !run);   // settings apply at launch
    InvalidateRect(g_status, nullptr, TRUE);
    if (run) { TailLog(); }
    UpdateUsage();
}

static void UpdateResourceLabels() {
    int mem = (int)SendMessageW(g_memSlider, TBM_GETPOS, 0, 0);
    int cpu = (int)SendMessageW(g_cpuSlider, TBM_GETPOS, 0, 0);
    wchar_t m[64]; swprintf(m, 64, L"Memory cap:  %d GB", mem); SetWindowTextW(g_memLbl, m);
    wchar_t c[64]; swprintf(c, 64, L"CPU cores:  %d / %d", cpu, g_maxCores); SetWindowTextW(g_cpuLbl, c);
}

static void DrawButton(LPDRAWITEMSTRUCT d, COLORREF accent, const wchar_t* text) {
    bool disabled = (d->itemState & ODS_DISABLED) != 0;
    bool pressed  = (d->itemState & ODS_SELECTED) != 0;
    COLORREF fill = disabled ? RGB(0x22, 0x22, 0x33) : (pressed ? accent :
        RGB(GetRValue(accent) * 9 / 10, GetGValue(accent) * 9 / 10, GetBValue(accent) * 9 / 10));
    HBRUSH b = CreateSolidBrush(fill); FillRect(d->hDC, &d->rcItem, b); DeleteObject(b);
    HPEN pen = CreatePen(PS_SOLID, 1, accent); HGDIOBJ op = SelectObject(d->hDC, pen);
    HGDIOBJ ob = SelectObject(d->hDC, GetStockObject(NULL_BRUSH));
    RoundRect(d->hDC, d->rcItem.left, d->rcItem.top, d->rcItem.right - 1, d->rcItem.bottom - 1, 8, 8);
    SelectObject(d->hDC, op); SelectObject(d->hDC, ob); DeleteObject(pen);
    SetBkMode(d->hDC, TRANSPARENT); SetTextColor(d->hDC, disabled ? kDim : kWhite);
    SelectObject(d->hDC, g_fBtn);
    DrawTextW(d->hDC, text, -1, &d->rcItem, DT_CENTER | DT_VCENTER | DT_SINGLELINE);
}

static HFONT MakeFont(int h, int weight, const wchar_t* face) {
    return CreateFontW(h, 0, 0, 0, weight, FALSE, FALSE, FALSE, DEFAULT_CHARSET,
                       OUT_DEFAULT_PRECIS, CLIP_DEFAULT_PRECIS, CLEARTYPE_QUALITY, DEFAULT_PITCH, face);
}

static LRESULT CALLBACK WndProc(HWND h, UINT m, WPARAM w, LPARAM l) {
    switch (m) {
    case WM_CREATE: {
        g_serverDir = DirOfSelf();
        g_serverExe = g_serverDir + L"\\nexus_realm.exe";
        g_logPath   = g_serverDir + L"\\nexus_realm.log";
        g_realmJson = g_serverDir + L"\\realm.json";
        LoadRealmInfo();
        SYSTEM_INFO sinfo; GetSystemInfo(&sinfo);
        g_maxCores = (int)sinfo.dwNumberOfProcessors; if (g_maxCores < 1) g_maxCores = 1;
        MEMORYSTATUSEX ms{ sizeof ms }; GlobalMemoryStatusEx(&ms);
        int ramGB = (int)(ms.ullTotalPhys / (1024ull * 1024ull * 1024ull));
        g_maxMemGB = ramGB > 16 ? 16 : (ramGB < 2 ? 2 : ramGB);
        g_cpuCores = g_maxCores; g_memGB = g_maxMemGB >= 4 ? 4 : g_maxMemGB;

        g_bgBrush = CreateSolidBrush(kBg); g_panelBrush = CreateSolidBrush(kPanel);
        g_fTitle = MakeFont(30, FW_HEAVY, L"Segoe UI"); g_fSub = MakeFont(15, FW_SEMIBOLD, L"Segoe UI");
        g_fBody = MakeFont(16, FW_NORMAL, L"Segoe UI"); g_fStatus = MakeFont(20, FW_BOLD, L"Segoe UI");
        g_fBtn = MakeFont(18, FW_SEMIBOLD, L"Segoe UI"); g_fMono = MakeFont(14, FW_NORMAL, L"Consolas");
        g_fSmall = MakeFont(14, FW_SEMIBOLD, L"Segoe UI");

        auto lbl = [&](int id, int x, int y, int cx, int cy, HFONT f, const wchar_t* t) {
            HWND w2 = CreateWindowW(L"STATIC", t, WS_CHILD | WS_VISIBLE, x, y, cx, cy, h, (HMENU)(INT_PTR)id, nullptr, nullptr);
            SendMessageW(w2, WM_SETFONT, (WPARAM)f, TRUE); return w2;
        };
        lbl(IDC_TITLE, 28, 20, 500, 40, g_fTitle, L"NEXUS UNLEASHED");
        lbl(IDC_SUB,   30, 58, 500, 22, g_fSub, L"SERVER LAUNCHER");
        g_status   = lbl(IDC_STATUS, 320, 28, 220, 32, g_fStatus, L"●  STOPPED");
        g_realmLbl = lbl(IDC_REALM, 30, 92, 520, 24, g_fBody, (L"Realm:  " + g_realmName).c_str());
        g_portsLbl = lbl(IDC_PORTS, 30, 118, 520, 22, g_fBody, g_ports.c_str());

        // resource governor
        g_memLbl = lbl(IDC_MEMLBL, 30, 156, 300, 20, g_fSmall, L"Memory cap:");
        g_memSlider = CreateWindowExW(0, TRACKBAR_CLASSW, L"", WS_CHILD | WS_VISIBLE | TBS_HORZ | TBS_NOTICKS,
                                      28, 178, 500, 30, h, (HMENU)IDC_MEMSLIDER, nullptr, nullptr);
        SendMessageW(g_memSlider, TBM_SETRANGE, TRUE, MAKELONG(1, g_maxMemGB));
        SendMessageW(g_memSlider, TBM_SETPOS, TRUE, g_memGB);
        g_cpuLbl = lbl(IDC_CPULBL, 30, 214, 300, 20, g_fSmall, L"CPU cores:");
        g_cpuSlider = CreateWindowExW(0, TRACKBAR_CLASSW, L"", WS_CHILD | WS_VISIBLE | TBS_HORZ | TBS_NOTICKS,
                                      28, 236, 500, 30, h, (HMENU)IDC_CPUSLIDER, nullptr, nullptr);
        SendMessageW(g_cpuSlider, TBM_SETRANGE, TRUE, MAKELONG(1, g_maxCores));
        SendMessageW(g_cpuSlider, TBM_SETPOS, TRUE, g_cpuCores);
        UpdateResourceLabels();

        g_start = CreateWindowW(L"BUTTON", L"START", WS_CHILD | WS_VISIBLE | BS_OWNERDRAW, 30, 278, 240, 50, h, (HMENU)IDC_START, nullptr, nullptr);
        g_stop  = CreateWindowW(L"BUTTON", L"STOP",  WS_CHILD | WS_VISIBLE | BS_OWNERDRAW, 290, 278, 240, 50, h, (HMENU)IDC_STOP, nullptr, nullptr);

        g_usage = lbl(IDC_USAGE, 30, 342, 500, 22, g_fMono, L"RAM  —     CPU  —");

        g_log = CreateWindowExW(WS_EX_CLIENTEDGE, L"EDIT", L"",
                                WS_CHILD | WS_VISIBLE | WS_VSCROLL | ES_MULTILINE | ES_READONLY | ES_AUTOVSCROLL,
                                30, 372, 500, 210, h, (HMENU)IDC_LOG, nullptr, nullptr);
        SendMessageW(g_log, WM_SETFONT, (WPARAM)g_fMono, TRUE);
        AppendLog(L"[launcher] Nexus Unleashed Server Launcher ready.");
        wchar_t hw[128]; swprintf(hw, 128, L"[launcher] this machine: %d logical cores, %d GB cap available.", g_maxCores, g_maxMemGB);
        AppendLog(hw);

        RefreshUI();
        SetTimer(h, IDT_POLL, 500, nullptr);
        return 0;
    }
    case WM_HSCROLL:
        UpdateResourceLabels();
        return 0;
    case WM_COMMAND:
        if (LOWORD(w) == IDC_START) StartServer();
        else if (LOWORD(w) == IDC_STOP) StopServer();
        RefreshUI();
        return 0;
    case WM_TIMER: RefreshUI(); return 0;
    case WM_DRAWITEM: {
        LPDRAWITEMSTRUCT d = (LPDRAWITEMSTRUCT)l;
        if (d->CtlID == IDC_START) DrawButton(d, kBlue, L"START");
        else if (d->CtlID == IDC_STOP) DrawButton(d, kMagenta, L"STOP");
        return TRUE;
    }
    case WM_CTLCOLORSTATIC: {
        HDC dc = (HDC)w; HWND ctl = (HWND)l; SetBkMode(dc, TRANSPARENT);
        int id = GetDlgCtrlID(ctl);
        if (id == IDC_TITLE) SetTextColor(dc, kMagenta);
        else if (id == IDC_SUB) SetTextColor(dc, kDim);
        else if (id == IDC_STATUS) SetTextColor(dc, IsRunning() ? kBlue : kDim);
        else if (id == IDC_USAGE) SetTextColor(dc, kBlue);
        else SetTextColor(dc, kMuted);
        return (LRESULT)g_bgBrush;
    }
    case WM_CTLCOLOREDIT: { HDC dc = (HDC)w; SetTextColor(dc, kMuted); SetBkColor(dc, kPanel); return (LRESULT)g_panelBrush; }
    case WM_CTLCOLORBTN: return (LRESULT)g_bgBrush;
    case WM_ERASEBKGND: { RECT rc; GetClientRect(h, &rc); FillRect((HDC)w, &rc, g_bgBrush); return 1; }
    case WM_CLOSE:
        if (IsRunning()) {
            if (MessageBoxW(h, L"The server is still running. Stop it and exit?", L"Nexus Unleashed Server Launcher",
                            MB_YESNO | MB_ICONQUESTION) != IDYES) return 0;
            StopServer();
        }
        DestroyWindow(h); return 0;
    case WM_DESTROY: KillTimer(h, IDT_POLL); PostQuitMessage(0); return 0;
    }
    return DefWindowProcW(h, m, w, l);
}

int WINAPI wWinMain(HINSTANCE hi, HINSTANCE, PWSTR, int show) {
    INITCOMMONCONTROLSEX icc{ sizeof icc, ICC_BAR_CLASSES }; InitCommonControlsEx(&icc);
    WNDCLASSW wc{}; wc.lpfnWndProc = WndProc; wc.hInstance = hi;
    wc.lpszClassName = L"NuslWindow"; wc.hCursor = LoadCursor(nullptr, IDC_ARROW);
    wc.hbrBackground = CreateSolidBrush(kBg);
    RegisterClassW(&wc);
    HWND h = CreateWindowW(wc.lpszClassName, L"Nexus Unleashed Server Launcher",
                           (WS_OVERLAPPEDWINDOW & ~WS_THICKFRAME & ~WS_MAXIMIZEBOX),
                           CW_USEDEFAULT, CW_USEDEFAULT, 576, 640, nullptr, nullptr, hi, nullptr);
    ShowWindow(h, show); UpdateWindow(h);
    MSG msg;
    while (GetMessageW(&msg, nullptr, 0, 0)) { TranslateMessage(&msg); DispatchMessageW(&msg); }
    return 0;
}
