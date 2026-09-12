/* TaskbarMatch.Xaml 1.1.2-beta.1
 * SPDX-License-Identifier: GPL-3.0-or-later
 * Taskbar-scoped XAML brush and contrast controller, x64 Windows 11.
 * No SDK, CRT, registry theme changes, network access or persistent hooks.
 * See the embedded build script for a reproducible clang/lld build.
 * ABI declarations: Microsoft's Windows SDK / win32metadata (XAML and xamlOM).
 * XAML diagnostics discovery approach also used by TranslucentTB and Windhawk.
 */

typedef unsigned char BYTE;
typedef unsigned short WCHAR;
typedef unsigned short WORD;
typedef unsigned int UINT, DWORD, ULONG;
typedef int BOOL, HRESULT, LONG;
typedef unsigned long long U64, UPTR, SIZE_T;
typedef long long I64, IPTR;
typedef void *HANDLE, *HWND, *HMODULE, *HHOOK, *HSTRING;
typedef WCHAR *BSTR;
typedef struct { DWORD a; WORD b,c; BYTE d[8]; } GUID;
typedef struct { void **v; } Obj;
typedef struct { BYTE A,R,G,B; } Color;
#define W(s) ((const WCHAR *)L##s)
#define API __declspec(dllimport)
#define EXP __declspec(dllexport)
#define OK ((HRESULT)0)
#define FALSE 0
#define TRUE 1
#define E_FAIL ((HRESULT)0x80004005u)
#define E_NOINTERFACE ((HRESULT)0x80004002u)
#define E_POINTER ((HRESULT)0x80004003u)
#define E_INVALIDARG ((HRESULT)0x80070057u)
#define E_PENDING ((HRESULT)0x8000000au)
#define E_ACCESSDENIED ((HRESULT)0x80070005u)
#define CLASS_E_CLASSNOTAVAILABLE ((HRESULT)0x80040111u)
#define CLASS_E_NOAGGREGATION ((HRESULT)0x80040110u)
#define FAILED(x) ((HRESULT)(x)<0)
#define SUCCEEDED(x) ((HRESULT)(x)>=0)
#define COM(o,n,type) ((type)((o)->v[n]))
#define WM_COPYDATA 0x4au
#define WM_SYNC 0x815au
#define MSG_MAGIC 0x45423131u
#define ACK 0x45420000u
#define NBUCKET 1024u
#define MAX_NODES 16384u
#define MAX_CONTEXTS 16u
#define MAX_BARS 32u

typedef IPTR (*WNDPROC)(HWND,UINT,UPTR,IPTR);
typedef struct { UINT cbSize,style; WNDPROC proc; int clsExtra,wndExtra; HMODULE inst;
 HANDLE icon,cursor,background; const WCHAR *menu,*name; HANDLE iconSmall; } WNDCLASSEX;
typedef struct { UPTR tag; DWORD count; void *data; } COPYDATA;
typedef struct { DWORD op,pid; U64 taskbar; DWORD abgr,theme,force,reserved; } Packet;
typedef struct { DWORD Size,Flags; } HIGHCONTRAST_PREFIX;

API HMODULE LoadLibraryExW(const WCHAR *,HANDLE,DWORD);
API HMODULE GetModuleHandleW(const WCHAR *);
API BOOL GetModuleHandleExW(DWORD,const WCHAR *,HMODULE *);
API void *GetProcAddress(HMODULE,const char *);
API DWORD GetModuleFileNameW(HMODULE,WCHAR *,DWORD);
API BOOL DisableThreadLibraryCalls(HMODULE);
API DWORD GetCurrentProcessId(void);
API DWORD GetCurrentThreadId(void);
API DWORD GetLastError(void);
API HANDLE GetProcessHeap(void);
API void *HeapAlloc(HANDLE,DWORD,SIZE_T);
API BOOL HeapFree(HANDLE,DWORD,void *);
API HANDLE CreateThread(void *,SIZE_T,DWORD(*)(void *),void *,DWORD,DWORD *);
API BOOL CloseHandle(HANDLE);
API DWORD WaitForSingleObject(HANDLE,DWORD);
API HANDLE OpenProcess(DWORD,BOOL,DWORD);
API void Sleep(DWORD);
API U64 GetTickCount64(void);
API void AcquireSRWLockExclusive(void **);
API void ReleaseSRWLockExclusive(void **);
API void AcquireSRWLockShared(void **);
API void ReleaseSRWLockShared(void **);
#ifdef _MSC_VER
#include <intrin.h>
#pragma intrinsic(_InterlockedCompareExchange)
#pragma intrinsic(_InterlockedExchange)
static LONG InterlockedCompareExchange(volatile LONG *p,LONG value,LONG old){return (LONG)_InterlockedCompareExchange((volatile long *)p,(long)value,(long)old);}
static LONG InterlockedExchange(volatile LONG *p,LONG value){return (LONG)_InterlockedExchange((volatile long *)p,(long)value);}
#else
static LONG InterlockedCompareExchange(volatile LONG *p,LONG value,LONG old){return __sync_val_compare_and_swap(p,old,value);}
static LONG InterlockedExchange(volatile LONG *p,LONG value){return __sync_lock_test_and_set(p,value);}
#endif
API int GetClassNameW(HWND,WCHAR *,int);
API DWORD GetWindowThreadProcessId(HWND,DWORD *);
API HWND GetAncestor(HWND,UINT);
API BOOL IsWindow(HWND);
API HWND FindWindowExW(HWND,HWND,const WCHAR *,const WCHAR *);
API HHOOK SetWindowsHookExW(int,IPTR(*)(int,UPTR,IPTR),HMODULE,DWORD);
API BOOL UnhookWindowsHookEx(HHOOK);
API IPTR CallNextHookEx(HHOOK,int,UPTR,IPTR);
API IPTR SendMessageTimeoutW(HWND,UINT,UPTR,IPTR,UINT,UINT,UPTR *);
API BOOL PostMessageW(HWND,UINT,UPTR,IPTR);
API WORD RegisterClassExW(const WNDCLASSEX *);
API HWND CreateWindowExW(DWORD,const WCHAR *,const WCHAR *,DWORD,int,int,int,int,HWND,HANDLE,HMODULE,void *);
API IPTR DefWindowProcW(HWND,UINT,UPTR,IPTR);
API IPTR SetWindowLongPtrW(HWND,int,IPTR);
API IPTR GetWindowLongPtrW(HWND,int);
API UPTR SetTimer(HWND,UPTR,UINT,void *);
API BOOL KillTimer(HWND,UPTR);
API void SysFreeString(BSTR);
API HRESULT RoInitialize(UINT);
API void RoUninitialize(void);
API HRESULT RoGetActivationFactory(HSTRING,const GUID *,void **);
API HRESULT WindowsCreateString(const WCHAR *,UINT,HSTRING *);
API HRESULT WindowsDeleteString(HSTRING);

#ifdef _MSC_VER
#define memset TaskbarMatch_memset
#define memcpy TaskbarMatch_memcpy
#endif
void *memset(void *dest,int value,SIZE_T n){volatile BYTE *d=(volatile BYTE *)dest;for(SIZE_T i=0;i<n;i++)d[i]=(BYTE)value;return dest;}
void *memcpy(void *dest,const void *src,SIZE_T n){volatile BYTE *d=(volatile BYTE *)dest;const volatile BYTE *s=(const volatile BYTE *)src;for(SIZE_T i=0;i<n;i++)d[i]=s[i];return dest;}

static HMODULE module;
static void *contextLock;
static volatile LONG started, ready;
static HRESULT attachError=E_PENDING;
static Obj *diag,*tree;
static WCHAR ownPath[1024];
static const WCHAR bridgeClass[]=L"TaskbarMatch.XamlBridge.1.1.2.b1";
static const GUID IID_Unknown={0,0,0,{0xc0,0,0,0,0,0,0,0x46}};
static const GUID IID_Agile={0x94ea2b94,0xe9cc,0x49e0,{0xc0,0xff,0xee,0x64,0xca,0x8f,0x5b,0x90}};
static const GUID IID_ClassFactory={1,0,0,{0xc0,0,0,0,0,0,0,0x46}};
static const GUID IID_Site={0xfc4801a3,0x2ba9,0x11cf,{0xa2,0x29,0,0xaa,0,0x3d,0x73,0x52}};
static const GUID CLSID_Tap={0xb4b3b358,0x79a7,0x4911,{0xac,0x84,0x92,0xb5,0x9d,0xd7,0xe1,0x11}};
static const GUID IID_Diag={0x18c9e2b6,0x3f43,0x4116,{0x9f,0x2b,0xff,0x93,0x5d,0x77,0x70,0xd2}};
static const GUID IID_Tree={0xa593b11a,0xd17f,0x48bb,{0x8f,0x66,0x83,0x91,0x07,0x31,0xc8,0xa5}};
static const GUID IID_Callback={0xaa7a8931,0x80e4,0x4fec,{0x8f,0x3b,0x55,0x3f,0x87,0xb4,0x96,0x6e}};
static const GUID IID_Callback2={0xbad9eb88,0xae77,0x4397,{0xb9,0x48,0x5f,0xa2,0xdb,0x0a,0x19,0xea}};
static const GUID IID_Source={0xd585bfe1,0x00ff,0x51be,{0xba,0x1d,0xa1,0x32,0x99,0x56,0xea,0x0a}};
static const GUID IID_SourceNative={0x3cbcf1bf,0x2f76,0x4e9c,{0x96,0xab,0xe8,0x4b,0x37,0x97,0x25,0x54}};
static const GUID IID_FE2={0xf19104be,0x422a,0x4904,{0xa5,0x2f,0xee,0x72,0x01,0x04,0x29,0xe5}};
static const GUID IID_FEStatics2={0x9695db02,0xc0d8,0x4fa2,{0xb1,0,0x3f,0xa2,0xdf,0x8b,0x95,0x38}};
static const GUID IID_Dep={0x5c526665,0xf60e,0x4912,{0xaf,0x59,0x5f,0xe0,0x68,0x0f,0x08,0x9d}};
static const GUID IID_DPStatics={0x49e5f28f,0x8259,0x4d5c,{0xaa,0xe0,0x83,0xd5,0x6d,0xbb,0x68,0xd9}};
static const GUID IID_ShapeStatics={0x1d7b4c55,0x9df3,0x48dc,{0x91,0x94,0x9d,0x30,0x6f,0xaa,0x60,0x89}};
static const GUID IID_Solid={0x9d850850,0x66f3,0x48df,{0x9a,0x8f,0x82,0x4b,0xd5,0xe0,0x70,0xaf}};
static const GUID IID_Reader={0x9891c6bd,0x534f,0x4955,{0xb8,0x5a,0x8a,0x8d,0xc0,0xdc,0xa6,0x02}};
static const GUID IID_Style={0xc4a9f225,0x9db7,0x4a7d,{0xb6,0xd1,0xf7,0x4e,0xdb,0x92,0x93,0xc2}};
static const GUID IID_Setter={0xa73ded29,0xb4ae,0x4a81,{0xbe,0x85,0xe6,0x90,0xba,0x0d,0x3b,0x6e}};
static const GUID IID_SetterVector={0xbd55a7fd,0x7501,0x5519,{0x9f,0xfb,0xd0,0x07,0x42,0x40,0xdb,0x61}};
static const GUID IID_Brush={0x8806a321,0x1e06,0x422c,{0xa1,0xcc,0x01,0x69,0x65,0x59,0xe0,0x21}};
static const GUID IID_SolidFactory={0xd935ce0c,0x86f5,0x4da6,{0x8a,0x27,0xb1,0x61,0x9e,0xf7,0xf9,0x2b}};

typedef HRESULT (*QI)(Obj *,const GUID *,void **);
typedef ULONG (*RefFn)(Obj *);
typedef HRESULT (*Getter)(Obj *,Obj **);
typedef HRESULT (*GetterInt)(Obj *,int *);
typedef HRESULT (*SetterInt)(Obj *,int);
typedef HRESULT (*DepGet)(Obj *,Obj *,Obj **);
typedef HRESULT (*DepSet)(Obj *,Obj *,Obj *);
typedef HRESULT (*DepClear)(Obj *,Obj *);
static ULONG unref(Obj *o){return o?COM(o,2,RefFn)(o):0;}
static HRESULT qi(Obj *o,const GUID *id,Obj **out){*out=0;return o?COM(o,0,QI)(o,id,(void **)out):E_POINTER;}
static BOOL eqguid(const GUID *a,const GUID *b){const BYTE *x=(const BYTE *)a,*y=(const BYTE *)b;for(UINT i=0;i<16;i++)if(x[i]!=y[i])return FALSE;return TRUE;}
static BOOL eqw(const WCHAR *a,const WCHAR *b){if(!a||!b)return a==b;while(*a&&*a==*b){a++;b++;}return *a==*b;}
static BOOL ends(const WCHAR *a,const WCHAR *b){if(!a)return FALSE;UINT x=0,y=0;while(a[x])x++;while(b[y])y++;return x>=y&&eqw(a+x-y,b);}
static BOOL contains(const WCHAR *s,const WCHAR *p){if(!s)return FALSE;for(;*s;s++){const WCHAR *a=s,*b=p;while(*a&&*a==*b){a++;b++;}if(!*b)return TRUE;}return FALSE;}
static UINT lenw(const WCHAR *s){UINT n=0;while(s[n])n++;return n;}
static void *alloc(SIZE_T n){return HeapAlloc(GetProcessHeap(),8,n);}
static void freep(void *p){if(p)HeapFree(GetProcessHeap(),0,p);}
static BOOL identity(Obj *a,Obj *b){if(a==b)return TRUE;if(!a||!b)return FALSE;Obj *x=0,*y=0;qi(a,&IID_Unknown,&x);qi(b,&IID_Unknown,&y);BOOL equal=x&&x==y;unref(x);unref(y);return equal;}
static HRESULT factory(const WCHAR *name,const GUID *id,Obj **out){HSTRING s=0;*out=0;HRESULT h=WindowsCreateString(name,lenw(name),&s);if(SUCCEEDED(h)){h=RoGetActivationFactory(s,id,(void **)out);WindowsDeleteString(s);}return h;}
static Obj *from_handle(U64 h){Obj *o=0;if(diag)COM(diag,6,HRESULT(*)(Obj *,U64,Obj **))(diag,h,&o);return o;}
static U64 to_handle(Obj *o){U64 h=0;if(diag&&o)COM(diag,7,HRESULT(*)(Obj *,Obj *,U64 *))(diag,o,&h);return h;}
static BOOL stock_bar(HWND w){WCHAR name[96];DWORD pid=0;if(!w)return FALSE;GetWindowThreadProcessId(w,&pid);if(pid!=GetCurrentProcessId())return FALSE;GetClassNameW(w,name,96);return eqw(name,W("Shell_TrayWnd"))||eqw(name,W("Shell_SecondaryTrayWnd"));}
static HRESULT new_brush(DWORD abgr,Obj **brush){Obj *f=0;*brush=0;HRESULT h=factory(W("Windows.UI.Xaml.Media.SolidColorBrush"),&IID_SolidFactory,&f);if(SUCCEEDED(h)){Color c={(BYTE)(abgr>>24),(BYTE)abgr,(BYTE)(abgr>>8),(BYTE)(abgr>>16)};h=COM(f,6,HRESULT(*)(Obj *,Color,Obj **))(f,c,brush);unref(f);}return h;}
static HRESULT brush_color(Obj *brush,DWORD abgr){Obj *b=0;HRESULT h=qi(brush,&IID_Solid,&b);if(SUCCEEDED(h)){Color c={(BYTE)(abgr>>24),(BYTE)abgr,(BYTE)(abgr>>8),(BYTE)(abgr>>16)};h=COM(b,7,HRESULT(*)(Obj *,Color))(b,c);unref(b);}return h;}

typedef struct {Obj *value; int saved,unset;} Snapshot;
typedef struct Node Node;
struct Node {Node *next,*hashNext;U64 id,parent;HWND bar;Obj *source,*dep,*fe;
 Snapshot theme,fill,foreground;UINT flags;int lastTheme,lastForeground,probed;};
#define F_SOURCE 1u
#define F_FILL 2u
#define F_STROKE 4u
#define F_BACKGROUND 8u
#define F_FRAME 16u
#define F_EXCLUDE 32u
#define F_TEXT 64u
#define F_ICON 128u
#define F_BADGE 256u
#define F_MAIN_BACKGROUND 512u
#define F_THEME_TARGET 1024u

typedef struct {HWND hwnd;DWORD abgr;int theme,active;Obj *brush,*clear;UINT fills,themes;} Bar;
typedef struct Delta {struct Delta *next;U64 id,parent;UINT flags;int mutation;} Delta;
typedef struct Context {DWORD tid;HWND window;Node *nodes,*buckets[NBUCKET];UINT count;
 Obj *themeDP,*fillDP,*unset,*textDP,*iconDP,*black,*white;Bar bars[MAX_BARS];UINT nBars;DWORD owner;HANDLE ownerProcess;
 int busy,dirty,posted,force;Delta *pending,*pendingLast;UINT pendingCount;} Context;
static Context *contexts[MAX_CONTEXTS];
static UINT contextCount;
static IPTR window_proc(HWND,UINT,UPTR,IPTR);
static void sync_context(Context *);
static void drain_changes(Context *);

static Node *find_node(Context *c,U64 id){UINT h=(UINT)((id>>4)^id)&(NBUCKET-1);for(Node *n=c->buckets[h];n;n=n->hashNext)if(n->id==id)return n;return 0;}
static Node *node(Context *c,U64 id){if(!id)return 0;Node *n=find_node(c,id);if(n)return n;if(c->count>=MAX_NODES)return 0;n=(Node *)alloc(sizeof(Node));if(!n)return 0;n->id=id;UINT h=(UINT)((id>>4)^id)&(NBUCKET-1);n->hashNext=c->buckets[h];c->buckets[h]=n;n->next=c->nodes;c->nodes=n;c->count++;return n;}
static HRESULT snapshot(Context *c,Obj *dep,Obj *dp,Snapshot *s){if(s->saved)return OK;if(!dep||!dp)return E_POINTER;HRESULT h=COM(dep,9,DepGet)(dep,dp,&s->value);if(SUCCEEDED(h)){s->saved=1;s->unset=identity(s->value,c->unset);}return h;}
static void restore_snapshot(Obj *dep,Obj *dp,Snapshot *s){if(!s->saved)return;if(s->unset)COM(dep,8,DepClear)(dep,dp);else COM(dep,7,DepSet)(dep,dp,s->value);unref(s->value);s->value=0;s->saved=0;s->unset=0;}
static void restore_node(Context *c,Node *n){if(n->dep){restore_snapshot(n->dep,c->themeDP,&n->theme);restore_snapshot(n->dep,c->fillDP,&n->fill);restore_snapshot(n->dep,(n->flags&F_TEXT)?c->textDP:c->iconDP,&n->foreground);}n->lastTheme=0;n->lastForeground=0;}
static void remove_node(Context *c,U64 id){Node *n=find_node(c,id);if(!n)return;restore_node(c,n);UINT h=(UINT)((id>>4)^id)&(NBUCKET-1);Node **p=&c->buckets[h];while(*p&&*p!=n)p=&(*p)->hashNext;if(*p)*p=n->hashNext;p=&c->nodes;while(*p&&*p!=n)p=&(*p)->next;if(*p)*p=n->next;unref(n->source);unref(n->dep);unref(n->fe);freep(n);c->count--;}
static void post_sync(Context *c){c->dirty=1;if(!c->posted&&c->window){c->posted=1;PostMessageW(c->window,WM_SYNC,0,0);}}
static Context *get_context(void){DWORD tid=GetCurrentThreadId();Context *c=0;AcquireSRWLockShared(&contextLock);for(UINT i=0;i<contextCount;i++)if(contexts[i]->tid==tid){c=contexts[i];break;}ReleaseSRWLockShared(&contextLock);if(c)return c;
 c=(Context *)alloc(sizeof(Context));if(!c)return 0;c->tid=tid;
 AcquireSRWLockExclusive(&contextLock);if(contextCount>=MAX_CONTEXTS){ReleaseSRWLockExclusive(&contextLock);freep(c);return 0;}contexts[contextCount++]=c;ReleaseSRWLockExclusive(&contextLock);
 WNDCLASSEX wc={0};wc.cbSize=sizeof(wc);wc.proc=window_proc;wc.inst=module;wc.name=bridgeClass;RegisterClassExW(&wc);
 c->window=CreateWindowExW(0x08000080u,bridgeClass,W("TaskbarMatch.XamlBridge"),0x80000000u,0,0,0,0,0,0,module,0);
 if(c->window)SetWindowLongPtrW(c->window,-21,(IPTR)c);return c;}
/* Obtain the standard dependency property through XAML's own metadata.
   This avoids hard-coding a version-dependent Control vtable. Styles are
   parsed once per UI thread, never in the color-sampling hot path. */
static HRESULT foreground_property(int icon,Obj **out){
 const WCHAR *xml=icon?
  W("<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='FontIcon'><Setter Property='Foreground' Value='Black'/></Style>"):
  W("<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='TextBlock'><Setter Property='Foreground' Value='Black'/></Style>");
 Obj *reader=0,*raw=0,*style=0,*collection=0,*vector=0,*item=0,*setter=0;HSTRING xaml=0;*out=0;
 HRESULT h=factory(W("Windows.UI.Xaml.Markup.XamlReader"),&IID_Reader,&reader);
 if(SUCCEEDED(h))h=WindowsCreateString(xml,lenw(xml),&xaml);
 if(SUCCEEDED(h))h=COM(reader,6,HRESULT(*)(Obj *,HSTRING,Obj **))(reader,xaml,&raw);
 if(SUCCEEDED(h))h=qi(raw,&IID_Style,&style);
 if(SUCCEEDED(h))h=COM(style,7,Getter)(style,&collection);
 if(SUCCEEDED(h))h=qi(collection,&IID_SetterVector,&vector);
 if(SUCCEEDED(h))h=COM(vector,6,HRESULT(*)(Obj *,UINT,Obj **))(vector,0,&item);
 if(SUCCEEDED(h))h=qi(item,&IID_Setter,&setter);
 if(SUCCEEDED(h))h=COM(setter,6,Getter)(setter,out);
 unref(setter);unref(item);unref(vector);unref(collection);unref(style);unref(raw);unref(reader);if(xaml)WindowsDeleteString(xaml);return h;
}
static HRESULT ensure_properties(Context *c){if(c->themeDP&&c->fillDP&&c->unset&&c->textDP&&c->iconDP&&c->black&&c->white)return OK;Obj *f=0;HRESULT h;
 if(!c->themeDP){h=factory(W("Windows.UI.Xaml.FrameworkElement"),&IID_FEStatics2,&f);if(FAILED(h))return h;h=COM(f,6,Getter)(f,&c->themeDP);unref(f);f=0;if(FAILED(h))return h;}
 if(!c->fillDP){h=factory(W("Windows.UI.Xaml.Shapes.Shape"),&IID_ShapeStatics,&f);if(FAILED(h))return h;h=COM(f,6,Getter)(f,&c->fillDP);unref(f);f=0;if(FAILED(h))return h;}
 if(!c->unset){h=factory(W("Windows.UI.Xaml.DependencyProperty"),&IID_DPStatics,&f);if(FAILED(h))return h;h=COM(f,6,Getter)(f,&c->unset);unref(f);if(FAILED(h))return h;}
 if(!c->textDP){h=foreground_property(0,&c->textDP);if(FAILED(h))return h;}
 if(!c->iconDP){h=foreground_property(1,&c->iconDP);if(FAILED(h))return h;}
 if(!c->black){h=new_brush(0xff000000u,&c->black);if(FAILED(h))return h;}
 if(!c->white){h=new_brush(0xffffffffu,&c->white);if(FAILED(h))return h;}
 return OK;}
static HWND node_bar(Context *c,Node *n,int *background){UINT visited=0;if(background)*background=0;while(n&&visited++<96){if(n->flags&F_EXCLUDE)return 0;if(background&&(n->flags&F_MAIN_BACKGROUND))*background=1;if(n->bar)return n->bar;n=find_node(c,n->parent);}return 0;}
static Bar *bar_state(Context *c,HWND hwnd,int create){for(UINT i=0;i<c->nBars;i++)if(c->bars[i].hwnd==hwnd)return &c->bars[i];if(!create||c->nBars>=MAX_BARS)return 0;Bar *b=&c->bars[c->nBars++];b->hwnd=hwnd;return b;}
static void map_sources(Context *c){for(Node *n=c->nodes;n;n=n->next){if(!(n->flags&F_SOURCE))continue;if(!n->source){Obj *o=from_handle(n->id);qi(o,&IID_Source,&n->source);unref(o);}if(!n->source)continue;Obj *native=0;HWND host=0;Obj *content=0;
 if(SUCCEEDED(qi(n->source,&IID_SourceNative,&native))){COM(native,4,HRESULT(*)(Obj *,HWND *))(native,&host);unref(native);}HWND bar=GetAncestor(host,2);if(!stock_bar(bar))continue;
 if(SUCCEEDED(COM(n->source,6,Getter)(n->source,&content))&&content){U64 root=to_handle(content);Node *r=node(c,root);if(r)r->bar=bar;bar_state(c,bar,1);}unref(content);}}
static void probe_node(Node *n){if(n->probed)return;Obj *o=from_handle(n->id);if(!o)return;n->probed=1;qi(o,&IID_Dep,&n->dep);qi(o,&IID_FE2,&n->fe);unref(o);}
static BOOL may_set_foreground(Context *c,Node *n){
 if(!(n->flags&(F_TEXT|F_ICON)))return FALSE;Node *p=n;UINT limit=0;
 while(p&&limit++<96){if(p->flags&F_BADGE)return FALSE;p=find_node(c,p->parent);}return TRUE;
}
static BOOL may_set_theme(Context *c,Node *n){
 if(!(n->flags&(F_TEXT|F_ICON|F_THEME_TARGET)))return FALSE;
 return node_bar(c,n,0)!=0;
}
static HRESULT save_foreground(Context *c,Node *n){
 if(n->foreground.saved)return OK;Obj *dp=(n->flags&F_TEXT)?c->textDP:c->iconDP;
 HRESULT h=snapshot(c,n->dep,dp,&n->foreground);if(FAILED(h))return h;
 /* Do not replace a BindingExpression. RequestedTheme still handles it. */
 if(n->foreground.value&&!n->foreground.unset){Obj *brush=0;h=qi(n->foreground.value,&IID_Brush,&brush);unref(brush);if(FAILED(h)){unref(n->foreground.value);n->foreground.value=0;n->foreground.saved=0;}}
 return h;
}
static void restore_bar(Context *c,HWND hwnd){Bar *b=bar_state(c,hwnd,0);if(b)b->active=0;for(Node *n=c->nodes;n;n=n->next)if(node_bar(c,n,0)==hwnd)restore_node(c,n);}
static void restore_all(Context *c){for(UINT i=0;i<c->nBars;i++)c->bars[i].active=0;for(Node *n=c->nodes;n;n=n->next)restore_node(c,n);if(c->ownerProcess){CloseHandle(c->ownerProcess);c->ownerProcess=0;}c->owner=0;KillTimer(c->window,1);}
static HRESULT claim(Context *c,DWORD pid){if(c->owner==pid&&c->ownerProcess)return OK;if(c->ownerProcess){if(WaitForSingleObject(c->ownerProcess,0)==0)restore_all(c);else return E_ACCESSDENIED;}HANDLE p=OpenProcess(0x100000u,FALSE,pid);if(!p)return E_ACCESSDENIED;if(WaitForSingleObject(p,0)==0){CloseHandle(p);return E_FAIL;}c->ownerProcess=p;c->owner=pid;SetTimer(c->window,1,1000,0);return OK;}
static void sync_context(Context *c){if(c->busy){post_sync(c);return;}c->busy=1;c->posted=0;drain_changes(c);
 if(c->dirty){map_sources(c);c->dirty=0;}int any=0;for(UINT i=0;i<c->nBars;i++)if(c->bars[i].active){any=1;break;}if(!any){c->busy=0;return;}if(FAILED(ensure_properties(c))){c->busy=0;return;}
 for(UINT i=0;i<c->nBars;i++){c->bars[i].fills=0;c->bars[i].themes=0;}
 /* Snapshot all main-background fills before changing local themes. */
 for(Node *n=c->nodes;n;n=n->next){int mainBackground=0;HWND hwnd=node_bar(c,n,&mainBackground);Bar *b=bar_state(c,hwnd,0);if(!b||!b->active||!mainBackground||!(n->flags&(F_FILL|F_STROKE)))continue;probe_node(n);if(n->dep)snapshot(c,n->dep,c->fillDP,&n->fill);}
 for(Node *n=c->nodes;n;n=n->next){HWND hwnd=node_bar(c,n,0);Bar *b=bar_state(c,hwnd,0);if(!b||!b->active||!may_set_foreground(c,n))continue;probe_node(n);if(n->dep)save_foreground(c,n);}
 for(Node *n=c->nodes;n;n=n->next){HWND hwnd=node_bar(c,n,0);Bar *b=bar_state(c,hwnd,0);if(!b||!b->active||!may_set_theme(c,n))continue;probe_node(n);if(n->fe&&n->dep){if(n->lastTheme!=b->theme||c->force){int current=0;HRESULT h=COM(n->fe,6,GetterInt)(n->fe,&current);if(SUCCEEDED(h)&&SUCCEEDED(snapshot(c,n->dep,c->themeDP,&n->theme))){if(current!=b->theme)h=COM(n->fe,7,SetterInt)(n->fe,b->theme);if(SUCCEEDED(h))n->lastTheme=b->theme;}}if(n->lastTheme==b->theme)b->themes++;}}
 /* Apply brushes after theme resources have changed, so the shell does not
    overwrite our background as part of the same light/dark transition. */
 for(Node *n=c->nodes;n;n=n->next){int mainBackground=0;HWND hwnd=node_bar(c,n,&mainBackground);Bar *b=bar_state(c,hwnd,0);if(!b||!b->active||!mainBackground||!n->dep||!n->fill.saved)continue;Obj *brush=(n->flags&F_FILL)?b->brush:((n->flags&F_STROKE)?b->clear:0);if(!brush)continue;Obj *now=0;HRESULT h=COM(n->dep,6,DepGet)(n->dep,c->fillDP,&now);if(SUCCEEDED(h)&&!identity(now,brush))h=COM(n->dep,7,DepSet)(n->dep,c->fillDP,brush);unref(now);if(SUCCEEDED(h)&&(n->flags&F_FILL))b->fills++;}
 /* Text and monochrome glyphs get explicit contrasting brushes as well.
    Raster application icons and colored notification badges are untouched. */
 for(Node *n=c->nodes;n;n=n->next){HWND hwnd=node_bar(c,n,0);Bar *b=bar_state(c,hwnd,0);if(!b||!b->active||!n->dep||!n->foreground.saved)continue;
  if(n->lastForeground!=b->theme||c->force){Obj *dp=(n->flags&F_TEXT)?c->textDP:c->iconDP;Obj *brush=b->theme==1?c->black:c->white;Obj *now=0;
   HRESULT h=COM(n->dep,6,DepGet)(n->dep,dp,&now);if(SUCCEEDED(h)&&!identity(now,brush))h=COM(n->dep,7,DepSet)(n->dep,dp,brush);unref(now);if(SUCCEEDED(h))n->lastForeground=b->theme;
  }
 }
 c->force=0;c->busy=0;}
static IPTR handle_packet(Context *c,HWND sender,const COPYDATA *cd){DWORD senderPid=0;if(!cd||cd->tag!=MSG_MAGIC||cd->count!=sizeof(Packet)||!cd->data)return 0;const Packet *p=(const Packet *)cd->data;GetWindowThreadProcessId(sender,&senderPid);if(!senderPid||senderPid!=p->pid)return 0;if(c->busy)return 0;
 if(p->op==0)return ACK|1u;
 if(p->op==3){if(c->owner==p->pid){c->busy=1;restore_all(c);c->busy=0;}return ACK|1u;}
 HWND hwnd=(HWND)(UPTR)p->taskbar;if(!stock_bar(hwnd))return 0;
 if(c->dirty)sync_context(c);Bar *b=bar_state(c,hwnd,0);if(!b)return 0;
 if(p->op==2){if(c->owner==p->pid){c->busy=1;restore_bar(c,hwnd);c->busy=0;}return ACK|1u;}
 if(p->op!=1||(p->theme!=1&&p->theme!=2)||FAILED(claim(c,p->pid)))return 0;
 HRESULT h=OK;if(!b->brush)h=new_brush(p->abgr,&b->brush);else if(b->abgr!=p->abgr)h=brush_color(b->brush,p->abgr);
 if(SUCCEEDED(h)&&!b->clear)h=new_brush(0,&b->clear);if(FAILED(h))return 0;
 b->active=1;b->abgr=p->abgr;b->theme=(int)p->theme;c->force=(int)p->force;sync_context(c);
 if(!b->fills||!b->themes){restore_bar(c,hwnd);return 0;}return ACK|1u;}
static IPTR window_proc(HWND hwnd,UINT msg,UPTR w,IPTR l){Context *c=(Context *)GetWindowLongPtrW(hwnd,-21);if(!c)return DefWindowProcW(hwnd,msg,w,l);
 if(msg==WM_COPYDATA)return handle_packet(c,(HWND)w,(const COPYDATA *)l);
 if(msg==WM_SYNC){sync_context(c);return 0;}
 if(msg==0x113u&&w==1){if(c->ownerProcess&&WaitForSingleObject(c->ownerProcess,0)==0){if(c->busy)post_sync(c);else{c->busy=1;restore_all(c);c->busy=0;}}return 0;}
 return DefWindowProcW(hwnd,msg,w,l);}

/* XAML diagnostic callback. Visual changes are queued; property writes occur
   only on the originating XAML UI thread, outside this callback. */
typedef struct {BSTR file;UINT line,column,position;BSTR hash;} SourceInfo;
typedef struct {U64 handle;SourceInfo source;BSTR type,name;UINT children;} VisualElement;
typedef struct {U64 parent,child;UINT index;} Relation;
static HRESULT callback_qi(Obj *self,const GUID *id,void **out){if(!out)return E_POINTER;*out=0;if(eqguid(id,&IID_Unknown)||eqguid(id,&IID_Callback)||eqguid(id,&IID_Callback2)||eqguid(id,&IID_Agile)){*out=self;return OK;}return E_NOINTERFACE;}
static ULONG immortal_ref(Obj *self){(void)self;return 2;}
static ULONG immortal_unref(Obj *self){(void)self;return 1;}
static void drain_changes(Context *c){
 Delta *d=c->pending;c->pending=0;c->pendingLast=0;c->pendingCount=0;
 while(d){Delta *next=d->next;
  if(d->mutation==1){
   /* Descendants must release their saved brushes even if diagnostics emits
      only a removal for the parent. Mark first, unlink in a second pass. */
   for(Node *n=c->nodes;n;n=n->next){Node *p=n;UINT limit=0;while(p&&limit++<96){if(p->id==d->id){n->flags|=0x80000000u;break;}p=find_node(c,p->parent);}}
   Node *n=c->nodes;while(n){Node *after=n->next;if(n->flags&0x80000000u)remove_node(c,n->id);n=after;}
  }else{Node *n=node(c,d->id);if(n){n->parent=d->parent;n->flags=d->flags;}}
  freep(d);d=next;
 }
}
static HRESULT visual_change(Obj *self,Relation relation,VisualElement e,int mutation){
 (void)self;Context *c=get_context();
 if(c&&c->pendingCount<MAX_NODES){Delta *d=(Delta *)alloc(sizeof(Delta));if(d){
  d->id=e.handle;d->parent=(mutation==0)?relation.parent:0;d->mutation=mutation;
  if(mutation==0){
   if(ends(e.type,W("DesktopWindowXamlSource")))d->flags|=F_SOURCE;
   if(ends(e.type,W(".TextBlock")))d->flags|=F_TEXT;
   if(ends(e.type,W(".FontIcon"))||ends(e.type,W(".SymbolIcon"))||ends(e.type,W(".PathIcon")))d->flags|=F_ICON;
   if(contains(e.type,W("Badge"))||contains(e.name,W("Badge"))||contains(e.name,W("Overlay")))d->flags|=F_BADGE;
   if(eqw(e.name,W("BackgroundFill")))d->flags|=F_FILL;
   if(eqw(e.name,W("BackgroundStroke")))d->flags|=F_STROKE;
   if(ends(e.type,W("Taskbar.TaskbarBackground"))){d->flags|=F_BACKGROUND;if(eqw(e.name,W("BackgroundControl")))d->flags|=F_MAIN_BACKGROUND;}
   if(ends(e.type,W("Taskbar.TaskbarFrame")))d->flags|=F_FRAME;
   if(contains(e.type,W("SearchUx."))||ends(e.type,W("Taskbar.SearchBoxButton"))||ends(e.type,W("Taskbar.ExperienceToggleButton"))||contains(e.name,W("SearchBoxButton")))d->flags|=F_THEME_TARGET;
   if(contains(e.type,W("Popup"))||contains(e.type,W("Flyout"))||contains(e.type,W("Thumbnail"))||contains(e.type,W("TaskItemThumbnail"))||contains(e.type,W("NotificationAreaOverflow"))||contains(e.name,W("HoverFlyout"))||contains(e.name,W("OverflowRootGrid"))||contains(e.name,W("OverflowFlyoutBackgroundBorder"))||contains(e.name,W("OverflowFlyout")))d->flags|=F_EXCLUDE;
  }
  if(c->pendingLast)c->pendingLast->next=d;else c->pending=d;c->pendingLast=d;c->pendingCount++;post_sync(c);
 }}
 /* Only Handle is valid in a Remove callback. */
 if(mutation==0){SysFreeString(e.source.file);SysFreeString(e.source.hash);SysFreeString(e.type);SysFreeString(e.name);}
 return OK;
}
static HRESULT visual_state(Obj *self,U64 id,int state,const WCHAR *text){(void)self;(void)id;(void)state;(void)text;return OK;}
static void *callback_vtbl[]={(void *)callback_qi,(void *)immortal_ref,(void *)immortal_unref,(void *)visual_change,(void *)visual_state};
static Obj callback={callback_vtbl};
static DWORD advise_thread(void *unused){(void)unused;HRESULT init=RoInitialize(1);HRESULT h=COM(tree,3,HRESULT(*)(Obj *,Obj *))(tree,&callback);attachError=h;if(SUCCEEDED(h))InterlockedExchange(&ready,1);if(SUCCEEDED(init))RoUninitialize();return 0;}
static HRESULT site_qi(Obj *self,const GUID *id,void **out){if(!out)return E_POINTER;*out=0;if(eqguid(id,&IID_Unknown)||eqguid(id,&IID_Site)||eqguid(id,&IID_Agile)){*out=self;return OK;}return E_NOINTERFACE;}
static HRESULT set_site(Obj *self,Obj *site){(void)self;if(!site)return OK;if(diag)return OK;Obj *d=0,*t=0;HRESULT h=qi(site,&IID_Diag,&d);if(SUCCEEDED(h))h=qi(site,&IID_Tree,&t);if(FAILED(h)){unref(d);unref(t);return h;}diag=d;tree=t;HANDLE th=CreateThread(0,0,advise_thread,0,0,0);if(!th)return E_FAIL;CloseHandle(th);return OK;}
static HRESULT get_site(Obj *self,const GUID *id,void **out){(void)self;if(!out)return E_POINTER;*out=0;return diag?COM(diag,0,QI)(diag,id,out):E_FAIL;}
static void *site_vtbl[]={(void *)site_qi,(void *)immortal_ref,(void *)immortal_unref,(void *)set_site,(void *)get_site};
static Obj site_object={site_vtbl};
static HRESULT cf_qi(Obj *self,const GUID *id,void **out){if(!out)return E_POINTER;*out=0;if(eqguid(id,&IID_Unknown)||eqguid(id,&IID_ClassFactory)){*out=self;return OK;}return E_NOINTERFACE;}
static HRESULT cf_create(Obj *self,Obj *outer,const GUID *id,void **out){(void)self;if(outer)return CLASS_E_NOAGGREGATION;return site_qi(&site_object,id,out);}
static HRESULT cf_lock(Obj *self,BOOL lock){(void)self;(void)lock;return OK;}
static void *factory_vtbl[]={(void *)cf_qi,(void *)immortal_ref,(void *)immortal_unref,(void *)cf_create,(void *)cf_lock};
static Obj class_factory={factory_vtbl};
EXP HRESULT DllGetClassObject(const GUID *cls,const GUID *id,void **out){if(!out)return E_POINTER;*out=0;if(!eqguid(cls,&CLSID_Tap))return CLASS_E_CLASSNOTAVAILABLE;return cf_qi(&class_factory,id,out);}
EXP HRESULT DllCanUnloadNow(void){return 1;}

typedef HRESULT (*InitializeDiag)(const WCHAR *,DWORD,const WCHAR *,const WCHAR *,GUID,const WCHAR *);
static InitializeDiag initialize;
typedef struct {UINT index;HRESULT result;} InitAttempt;
static DWORD init_attempt(void *arg){InitAttempt *a=(InitAttempt *)arg;WCHAR name[64];const WCHAR *base=W("VisualDiagConnection");UINT k=0;while(base[k]){name[k]=base[k];k++;}UINT v=a->index;WCHAR digits[12];UINT n=0;do{digits[n++]=(WCHAR)('0'+v%10);v/=10;}while(v);while(n)name[k++]=digits[--n];name[k]=0;HRESULT ri=RoInitialize(1);a->result=initialize(name,GetCurrentProcessId(),W(""),ownPath,CLSID_Tap,W("TaskbarMatch 1.1.2"));if(SUCCEEDED(ri))RoUninitialize();return 0;}
static DWORD initialize_thread(void *unused){(void)unused;HMODULE xaml=LoadLibraryExW(W("Windows.UI.Xaml.dll"),0,0x800u);if(!xaml){attachError=E_FAIL;return 0;}initialize=(InitializeDiag)GetProcAddress(xaml,"InitializeXamlDiagnosticsEx");if(!initialize){attachError=E_NOINTERFACE;return 0;}
 for(UINT i=1;i<=60;i++){InitAttempt a={i,E_PENDING};HANDLE th=CreateThread(0,0,init_attempt,&a,0,0);if(!th){attachError=E_FAIL;return 0;}WaitForSingleObject(th,0xffffffffu);CloseHandle(th);if(SUCCEEDED(a.result)){attachError=OK;return 0;}if(a.result!=(HRESULT)0x80070490u){attachError=a.result;return 0;}}attachError=(HRESULT)0x80070490u;return 0;}
static IPTR hook_proc(int code,UPTR w,IPTR l){if(code>=0&&InterlockedCompareExchange(&started,1,0)==0){HMODULE pin=0;GetModuleHandleExW(5,(const WCHAR *)(UPTR)&hook_proc,&pin);GetModuleFileNameW(module,ownPath,1024);HANDLE th=CreateThread(0,0,initialize_thread,0,0,0);if(th)CloseHandle(th);else attachError=E_FAIL;}return CallNextHookEx(0,code,w,l);}

/* These two exports run in the controller process, not inside Explorer. */
EXP HRESULT EB_EnsureInjected(HWND taskbar){DWORD pid=0,tid=GetWindowThreadProcessId(taskbar,&pid);if(!pid||!tid||pid==GetCurrentProcessId())return E_INVALIDARG;
 HWND bridge=0;while((bridge=FindWindowExW(0,bridge,bridgeClass,0))!=0){DWORD p=0;GetWindowThreadProcessId(bridge,&p);if(p==pid)return OK;}
 static DWORD lastPid;static U64 lastAttempt;U64 now=GetTickCount64();if(lastPid==pid&&now-lastAttempt<1500)return E_PENDING;lastPid=pid;lastAttempt=now;
 HHOOK hook=SetWindowsHookExW(4,hook_proc,module,tid);if(!hook)return E_ACCESSDENIED;UPTR r=0;SendMessageTimeoutW(taskbar,0,0,0,3,500,&r);UnhookWindowsHookEx(hook);return E_PENDING;}
EXP HRESULT EB_Command(DWORD explorerPid,HWND sender,DWORD op,HWND taskbar,DWORD abgr,DWORD theme,DWORD force){Packet p={op,GetCurrentProcessId(),(U64)(UPTR)taskbar,abgr,theme,force,0};COPYDATA cd={MSG_MAGIC,sizeof(p),&p};HWND w=0;int accepted=0;
 while((w=FindWindowExW(0,w,bridgeClass,0))!=0){DWORD pid=0;GetWindowThreadProcessId(w,&pid);if(pid!=explorerPid)continue;UPTR result=0;if(SendMessageTimeoutW(w,WM_COPYDATA,(UPTR)sender,(IPTR)&cd,3,500,&result)&&((result&0xffff0000u)==ACK))accepted=1;}
 return accepted?OK:E_PENDING;}
BOOL DllMain(HMODULE instance,DWORD why,void *reserved){(void)reserved;if(why==1){module=instance;DisableThreadLibraryCalls(instance);}return TRUE;}

/* ABI assertions checked during the real x64 PE build. */
_Static_assert(sizeof(void *)==8,"x64 only");
_Static_assert(sizeof(GUID)==16,"GUID ABI");
_Static_assert(sizeof(Color)==4,"Color ABI");
_Static_assert(sizeof(Packet)==32,"IPC ABI");
_Static_assert(sizeof(SourceInfo)==32,"SourceInfo ABI");
_Static_assert(sizeof(Relation)==24,"Relation ABI");
_Static_assert(sizeof(VisualElement)==64,"VisualElement ABI");
_Static_assert(sizeof(WNDCLASSEX)==80,"WNDCLASSEXW ABI");
