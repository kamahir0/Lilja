using System.Runtime.CompilerServices;

// エディタアセンブリが internal な型（DebugMenuCore, HostRegistry, IPageHost など）にアクセスできるようにする
[assembly: InternalsVisibleTo("Lilja.DebugUI.Editor")]
