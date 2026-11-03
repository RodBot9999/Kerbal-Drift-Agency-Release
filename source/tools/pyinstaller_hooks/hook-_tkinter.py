from pathlib import Path
import sys
runtime=Path(sys.base_prefix)
datas=[(str(runtime/'tcl/tcl8.6'),'_tcl_data'),(str(runtime/'tcl/tk8.6'),'_tk_data'),(str(runtime/'tcl/tcl8'),'tcl8')]
binaries=[(str(runtime/'DLLs/tcl86t.dll'),'.'),(str(runtime/'DLLs/tk86t.dll'),'.')]
