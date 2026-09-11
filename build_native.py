from pathlib import Path
import subprocess, hashlib
p = Path(__file__).resolve().parent
imports = {
    'kernel32': '''LoadLibraryExW GetModuleHandleW GetModuleHandleExW GetProcAddress GetModuleFileNameW DisableThreadLibraryCalls GetCurrentProcessId GetCurrentThreadId GetLastError GetProcessHeap HeapAlloc HeapFree CreateThread CloseHandle WaitForSingleObject OpenProcess Sleep GetTickCount64 AcquireSRWLockExclusive ReleaseSRWLockExclusive AcquireSRWLockShared ReleaseSRWLockShared'''.split(),
    'user32': '''GetClassNameW GetWindowThreadProcessId GetAncestor IsWindow FindWindowExW SetWindowsHookExW UnhookWindowsHookEx CallNextHookEx SendMessageTimeoutW PostMessageW RegisterClassExW CreateWindowExW DefWindowProcW SetWindowLongPtrW GetWindowLongPtrW SetTimer KillTimer'''.split(),
    'oleaut32': ['SysFreeString'],
    'combase': ['RoInitialize', 'RoUninitialize', 'RoGetActivationFactory', 'WindowsCreateString', 'WindowsDeleteString'],
}
for name, functions in imports.items():
    f=p/(name+'.def')
    f.write_text('LIBRARY '+name+'.dll\nEXPORTS\n'+'\n'.join(functions)+'\n')
    subprocess.run(['lld-link','/dll','/noentry','/machine:x64',f'/def:{f}',f'/out:{p/name}.stub.dll',f'/implib:{p/name}.lib','/nodefaultlib'],check=True,capture_output=True)
subprocess.run(['clang','--target=x86_64-pc-windows-msvc','-std=c11','-Os','-g0','-ffreestanding','-fno-builtin','-fno-stack-protector','-funwind-tables','-Wall','-Wextra','-Werror','-c',str(p/'TaskbarMatch.Xaml.c'),'-o',str(p/'TaskbarMatch.Xaml.obj')],check=True)
subprocess.run(['lld-link','/dll','/entry:DllMain','/machine:x64','/nodefaultlib','/dynamicbase','/nxcompat','/highentropyva','/opt:ref','/opt:icf','/release','/timestamp:0',f'/out:{p/"TaskbarMatch.Xaml.dll"}',str(p/'TaskbarMatch.Xaml.obj')]+[str(p/(n+'.lib')) for n in imports],check=True)
b = (p/'TaskbarMatch.Xaml.dll').read_bytes()
print(len(b),hashlib.sha256(b).hexdigest())
