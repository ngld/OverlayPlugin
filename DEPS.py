# If you're familiar with Chromium or other Google projects:
#   Sorry, this file actually has nothing to do with gclient.
#   I just borrowed the basic concept and naming scheme.

deps = {
    'ACT': {
        'url': 'https://github.com/EQAditu/AdvancedCombatTracker/releases/download/3.5.0.273/ACTv3.zip',
        'dest': 'Thirdparty/ACT',
        'strip': 0,
        'hash': ['sha256', 'b69281760ae3ceb33d8b45a299c9ed5c90668544664b9ade373f548dbb0e86d6'],
    },
    'FFXIV_ACT_Plugin': {
        'url': 'https://github.com/ravahn/FFXIV_ACT_Plugin/raw/master/Releases/FFXIV_ACT_Plugin_SDK_2.6.2.7.zip',
        'dest': 'Thirdparty/FFXIV_ACT_Plugin',
        'strip': 0,
        'hash': ['sha256', '05a925113960889722497b6c7195dc899358a13a73d98a08d4a8ab04ef778605'],
    },
    'curl': {
        'url': 'https://curl.haxx.se/download/curl-7.70.0.tar.xz',
        'dest': 'Thirdparty/curl',
        'strip': 1,
        'hash': ['sha256', '7f84eb2f9409591cb0132cb4b5975be7680eb6daa73d92711880569f86db355b'],
    },
}
