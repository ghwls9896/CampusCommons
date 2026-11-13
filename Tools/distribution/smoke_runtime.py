"""Offline smoke test using synthetic pixels, never a webcam or personal image."""
from io import BytesIO
from pathlib import Path
import sys
root=Path(sys.argv[1]).resolve()
assert Path(sys.executable).resolve().is_relative_to(root), 'Used external Python'
assert Path(sys.prefix).resolve().is_relative_to(root), 'Used external Python prefix'
sys.path.insert(0,str(root/'app'))
from PIL import Image
from qev_backend import QEVBackend
model=QEVBackend.load(root/'model',root/'hf-cache/hub')
image=Image.new('RGB',(224,224),'white');buf=BytesIO();image.save(buf,format='JPEG')
result=model.classify(buf.getvalue())
assert result['ok'] and result['mode']=='fast'
print('RELOCATED_QEV_OFFLINE_PASS platform='+sys.platform+' device='+result['device'],flush=True)
