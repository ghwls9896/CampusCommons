"""Offline smoke test using synthetic pixels, never a webcam or personal image."""
from io import BytesIO
from pathlib import Path
import sys
root=Path(sys.argv[1]).resolve()
assert Path(sys.executable).resolve().is_relative_to(root), 'Used external Python'
assert Path(sys.prefix).resolve().is_relative_to(root), 'Used external Python prefix'
sys.path.insert(0,str(root/'app'))
from PIL import Image
from desktop_capture import desktop_provider
from qev_backend import QEVBackend
from screen_study import combine
assert desktop_provider() is not None, 'Window capture component is missing'
model=QEVBackend.load(root/'model',root/'hf-cache/hub')
image=Image.new('RGB',(224,224),'white');buf=BytesIO();image.save(buf,format='JPEG')
result=model.classify(buf.getvalue())
assert result['ok'] and result['mode']=='fast'
camera = model.classify_pose(buf.getvalue())
assert camera['observed'] is False, 'A blank camera frame must not count as study'
image.paste((45, 56, 79), (0, 0, 100, 100))
with BytesIO() as buf:
    image.save(buf, format='JPEG')
    data = buf.getvalue()
paired = combine(model.classify_pose(data)['probabilities'], 'unknown')
assert not paired['observed'] or abs(sum(paired['probabilities'].values()) - 1) <= .001
assert model.classify_screen(data) in ('study', 'other', 'unknown')
print('RELOCATED_QEV_OFFLINE_PASS platform='+sys.platform+' device='+result['device'],flush=True)
