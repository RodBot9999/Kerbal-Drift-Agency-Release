# The build sandbox cannot initialize native Tcl. Keep tkinter discoverable;
# the adjacent _tkinter hook collects the known CPython runtime directly.
def pre_find_module_path(hook_api):
    pass
