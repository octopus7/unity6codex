"""Data-level checks; does not claim browser UI verification."""
import json, math
from pathlib import Path
p=Path(__file__).resolve().parent
rig=json.loads((p/'rig.json').read_text())
expected=['front','side','rear','side']
for name in ['torso','pelvis']:
    seq=[rig['samples'][i]['nodes'][name]['art'].split('/')[0] for i in [0,30,60,90]]
    assert seq==expected,(name,seq)
    values=[s['nodes'][name]['art'].split('/')[0] for s in rig['samples']]
    for a,b in zip(values,values[1:]+values[:1]):
        assert {a,b}!={'front','rear'},(name,a,b)
for sample in rig['samples']:
    for name,node in sample['nodes'].items():
        assert node['art'] in rig['parts'],node['art']
        assert (p/rig['parts'][node['art']]['file']).is_file()
        assert all(math.isfinite(x) for x in [*node['xy'],node['angle']])
        visited={name}; parent=node['parent']
        while parent:
            assert parent in sample['nodes'] and parent not in visited
            visited.add(parent);parent=sample['nodes'][parent]['parent']
    assert sample['nodes']['head']['art']=='front/head'
    assert sample['nodes']['bow']['parent']=='near_hand'
    for side in ['near','far']:
        for limb in ['thigh','shin','foot','upper_arm','forearm','hand']:
            assert sample['nodes'][side+'_'+limb]['art'].endswith('/'+side+'_'+limb)
v=json.loads((p/'validation.json').read_text())
assert v['loop_endpoint_equal']
assert v['max_joint_length_error_px']<1e-8
assert v['max_stance_ankle_height_error_px']<1e-8
assert v['max_bow_grip_attachment_error_px']<1e-8
print('PASS: 120 samples, three-view sequence, no direct torso/pelvis front-rear swap, asset references, identity, hierarchy, attachment metrics.')

