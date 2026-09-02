"""Importing this module puts every tools/ subfolder on sys.path, so the dev tools import each other by
bare module name regardless of which subfolder they live in. Entry scripts start with:

    import os as _os, sys as _sys
    _sys.path.insert(0, _os.path.join(_os.path.dirname(_os.path.abspath(__file__)), '..', 'lib'))
    import toolpath  # noqa: F401
"""
import os, sys
_TOOLS = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
for _d in ('lib', 'queens', 'brownboo', 'yellowdrops', 'darkshrine', 'stubs', 'analysis',
           'iso_patch', os.path.join('iso_patch', 'collision')):
    _p = os.path.join(_TOOLS, _d)
    if os.path.isdir(_p) and _p not in sys.path:
        sys.path.insert(0, _p)
