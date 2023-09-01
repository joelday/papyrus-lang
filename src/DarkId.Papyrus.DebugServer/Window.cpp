#include "Window.h"
#include <Windows.h>

struct handle_data {
    unsigned long process_id;
    HWND window_handle;
};

static BOOL is_main_window(HWND handle)
{
    return GetWindow(handle, GW_OWNER) == (HWND)0 && IsWindowVisible(handle);
}

 static BOOL enum_windows_callback(HWND handle, LPARAM lParam)
 {
     handle_data& data = *(handle_data*)lParam;
     unsigned long process_id = 0;
     GetWindowThreadProcessId(handle, &process_id);
     if (data.process_id != process_id || !is_main_window(handle))
         return TRUE;
     data.window_handle = handle;
     return FALSE;
 }

 static HWND find_main_window(unsigned long process_id)
 {
     handle_data data;
     data.process_id = process_id;
     data.window_handle = 0;
     EnumWindows(enum_windows_callback, (LPARAM)&data);
     return data.window_handle;
 }

 static HWND get_main_window_handle() {
     return find_main_window(GetCurrentProcessId());
 }

 HWND GetWindowHandleFromRenderWindow() {
#if SKYRIM
	 auto window = RE::BSGraphics::UTIL::GetCurrentRenderWindow();
#else
	 REL::Relocation<uintptr_t> pCurrentRenderWindow(REL::ID(91810));
	 auto window = *(RE::BSGraphics::RendererWindow**)pCurrentRenderWindow.address();
#endif
	 return stl::unrestricted_cast<HWND>(window->hwnd);
 }


 void Window::ReleaseFocus() {
#if SKYRIM
     auto deviceManager = RE::BSInputDeviceManager::GetSingleton();
     auto mouse = deviceManager->GetMouse();
     mouse->Release();
     RE::MenuCursor::GetSingleton()->SetCursorVisibility(true);
#endif // Fallout does not have the problem of not letting the mouse cursor go when the focus is lost, so no need
 }


 void Window::RegainFocus() {
	 HWND handle = get_main_window_handle();
	 ShowWindow(handle, SW_SHOW);
	 SetForegroundWindow(handle);
	 SetFocus(handle);
#if SKYRIM // Need to reinit mouse since we lost it
     RE::MenuCursor::GetSingleton()->SetCursorVisibility(false);
     auto deviceManager = RE::BSInputDeviceManager::GetSingleton();
     deviceManager->ReinitializeMouse();
#endif
 }