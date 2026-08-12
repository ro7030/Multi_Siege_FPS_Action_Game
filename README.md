# Multi Siege FPS Action Game

멀티플레이어 공성(시즈) FPS 액션 게임입니다.  
플레이어가 성문을 방어하며 웨이브 적군을 막아내고, 준비 페이즈에 농장·상점·방어 시설을 활용해 다음 웨이브에 대비합니다.

> **프로젝트 상태:** 개발 종료 (아카이브)

---

## 시현 영상

1팀 몽키 가드 시현 영상입니다.

https://github.com/ro7030/Multi_Siege_FPS_Action_Game/raw/main/docs/media/monkey_guard_demo.mp4

로컬 파일: [`docs/media/monkey_guard_demo.mp4`](docs/media/monkey_guard_demo.mp4)

---

## 개요

| 항목 | 내용 |
|------|------|
| 장르 | 멀티플레이어 공성 FPS / 웨이브 디펜스 |
| 엔진 | Unity **6000.3.11f1** (Unity 6) |
| 렌더 파이프라인 | URP |
| 네트워킹 | Netcode for GameObjects + Unity Lobby / Relay |
| 인증 | Unity Authentication (게스트 / Unity Player Accounts) |
| 데이터 | 로컬 MySQL (세션 결과·플레이어 통계 저장) |
| 최대 인원 | 방당 최대 **4명** |
| 웨이브 | 기본 최대 **10웨이브** |

---

## 주요 기능

### 멀티플레이 · 세션
- Unity Lobby / Relay 기반 방 생성·참가 (비밀번호 지원)
- Host / Client 세션 동기화 (`NetworkMatchDirector`)
- 매치 종료 후 리매치 의도 집계

### 게임 루프
페이즈 흐름:

`Lobby` → `Preparation` → `Wave` → `WaveCleared` → (반복) → `Result`

- **Preparation:** 준비 시간 동안 상점, 농장, 성문/방어물 설치·수리
- **Wave:** 적 스폰 및 전투 / 성문 방어
- **Result:** 클리어·실패 결과 및 통계 표시

### 전투 · 플레이어
- 1인칭 이동·사격·근접 무기
- 무기 키트 / 투척물 / 힐 키트 인벤토리
- 체력, 기절, 부활(Revive) 시스템
- 팀원 체력 HUD, 스코어보드, 크로스헤어 등 UI

### 방어 · 경제
- 성문(Gate) 및 방어 오브젝트 HP / 설치·수리
- 농장 수확 → 재화 → 상점 구매
- 웨이브·성과 기반 보상 계산

### 인증 · 데이터
- 게스트(Anonymous) / Unity Player Account 로그인
- 매치 종료 시 MySQL에 세션 결과·개인 통계 업로드

---

## 씬 구성

| 씬 | 경로 | 역할 |
|----|------|------|
| Login | `Assets/Scenes/Login.unity` | 인증·로그인 |
| MainMenu | `Assets/Scenes/MainMenu.unity` | 메인 메뉴, 방 생성/참가 |
| CharacterSelect | `Assets/Scenes/CharacterSelect.unity` | 캐릭터 선택 |
| GamePlay | `Assets/Scenes/GamePlay.unity` | 본 게임플레이 |

권장 진입 순서: **Login → MainMenu → CharacterSelect → GamePlay**

---

## 폴더 구조 (스크립트)

게임 로직은 `Assets/Settings/Script/` 아래에 모듈별로 나뉘어 있습니다.  
네임스페이스 접두사: `ProjectM.*`

```
Assets/Settings/Script/
├── Auth/            # UGS Authentication
├── Audio/           # BGM, 총성, 발소리, UI 사운드
├── CharacterSelect/ # 캐릭터 선택
├── Combat/          # 전투 보조 (스턴, 화염 존 등)
├── Core/            # 매치 상태, 페이즈, 세션 매니저
├── Data/            # MySQL / DTO / 결과 업로드
├── Defense/         # 성문, 방어물, 농장 플롯
├── Economy/         # 지갑, 상점, 수확, 보상
├── Enemy/           # 적 AI, 스폰, 스탯
├── Network/         # NGO 브릿지, 로비/릴레이, 매치 디렉터
├── Player/          # 조작, 무기, 인벤, 체력, 부활
├── UI/              # HUD, 로비, 상점, 결과 화면 등
└── Wave/            # 웨이브 설정·매니저
```

기타:
- `Assets/Scenes/` — 게임 씬
- `Assets/Database/` — 로컬 MySQL 초기화 스크립트
- `Assets/Prefab/Network/` — 네트워크 플레이어 등 프리팹
- `Assets/Resources/` — UI·사운드·애니메이션 리소스

---

## 기술 스택 (주요 패키지)

| 패키지 | 용도 |
|--------|------|
| `com.unity.netcode.gameobjects` | 멀티플레이 동기화 |
| `com.unity.services.multiplayer` | Lobby / Relay |
| `com.unity.services.authentication` | 로그인 |
| `com.unity.transport` | UTP 전송 |
| `com.unity.inputsystem` | 입력 |
| `com.unity.render-pipelines.universal` | URP 렌더링 |
| `com.veriorpies.parrelsync` | 에디터 멀티 인스턴스 테스트 |
| NuGetForUnity + MySqlConnector | 로컬 DB 연동 |

---

## 로컬 실행 방법

### 1. Unity
1. Unity Hub에서 **Unity 6000.3.11f1**로 프로젝트 오픈
2. UGS(Lobby / Relay / Authentication) 프로젝트 연동 확인
3. `Login` 씬부터 Play, 또는 빌드 후 실행

### 2. 멀티플레이 테스트
- **ParrelSync**로 클론 프로젝트를 띄워 Host / Client 동시 테스트 가능
- 또는 빌드 실행 파일 + 에디터 조합으로 테스트

### 3. MySQL (선택 — 결과 저장용)

```bash
# Assets/Database/setup_local_mysql.sh
# root 비밀번호 입력 후 DB/계정/테이블 생성
bash Assets/Database/setup_local_mysql.sh
```

기본 연결 정보 (개발용):

```
Server=127.0.0.1;Port=3306;User ID=game_dev;Password=game_dev;Database=multi_siege_fps;
```

생성 테이블:
- `session_results` — 매치 단위 결과
- `player_stats` — 플레이어별 킬/수확/수리/부활/데미지 등

---

## 아키텍처 요약

```
[Login / Auth]
      ↓
[MainMenu · LobbyRelayService] ── Lobby + Relay Join Code
      ↓
[CharacterSelect · Loadout]
      ↓
[GamePlay]
  ├─ GameSessionManager + PhaseController   (로컬 매치 상태)
  ├─ NetworkMatchDirector                   (서버→클라 페이즈/웨이브 미러링)
  ├─ WaveManager + EnemySpawner             (Host 권한 스폰)
  ├─ NetworkPlayer / Damage / Gate bridges  (전투·방어 동기화)
  └─ ResultUploader → MySQL                 (종료 시 통계 저장)
```

Host(서버)가 웨이브·페이즈·적 스폰·데미지 권한을 갖고, 클라이언트는 `NetworkVariable` / RPC 브릿지로 UI와 로컬 표현을 맞춥니다.

---

## 개발 메모

- 스크립트 언어: **C#**, 네임스페이스 `ProjectM`
- 제품명(ProjectSettings): `Test` / 버전 `0.1.0` (에디터 설정값)
- 에셋 스토어·서드파티 리소스(캐릭터, FX, 크로스헤어, 맵 프랍 등)가 `Assets/` 하위에 포함되어 있습니다. 재배포 시 각 라이선스를 확인하세요.

---

## 라이선스

프로젝트 코드·에셋의 공개 범위와 라이선스는 팀 정책에 따릅니다.  
서드파티 에셋은 각 패키지 README / License 파일을 참고하세요.
