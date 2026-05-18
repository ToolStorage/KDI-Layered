# KDI Layered Architecture

Unity 6 전용. Kylin DI 위에 5-layer 단방향 의존을 강제하는 얇은 마커·검증 레이어. 게임 개발에서 순환 참조·상호 참조·초기화 순서 문제를 구조적으로 회피한다.

```
com.kylin.di.layered | Unity 6000.0+ | MIT License
```

---

## 핵심 원칙

5개의 레이어로 코드를 나눈다. **상위 레이어만이 하위 레이어를 주입할 수 있고**, 같은 레이어 간 주입 및 역방향 주입은 컴파일 가능하더라도 `LayerValidator`가 빌드 타임에 거절한다.

```
View                  ← 가장 유저에 가까운 UI/시각화 (MonoBehaviour)
 │ injects
 ▼
ViewModel             ← View가 사용할 값과 비즈니스 로직 (singleton 또는 per-view)
 │ injects
 ▼
ApplicationService    ← 여러 Data·DomainService를 조율하는 단일 작업 단위
 │ injects
 ▼
DomainService         ← 단일 Data를 변경하는 책임자 (다른 Data는 readonly로만 참조)
 │ injects
 ▼
Data                  ← SubscribableProperty 보유, 외부 노출은 IReadOnly로만
```

**금지되는 의존**:
- 같은 레이어끼리 주입 (ViewModel A → ViewModel B 등)
- 하위 레이어가 상위 레이어를 주입
- 한 칸 건너뛰기는 허용 (예: View가 DomainService 직접 주입)

---

## 설치

`Packages/manifest.json`에 추가:

```json
{
  "scopedRegistries": [
    {
      "name": "Kylin",
      "url": "https://registry.npmjs.org",
      "scopes": ["com.kylin"]
    }
  ],
  "dependencies": {
    "com.kylin.di.layered": "1.0.0"
  }
}
```

의존성 `com.kylin.di`, `com.kylin.subscribable`은 자동으로 함께 설치된다.

---

## 레이어 정의

각 레이어 마커는 해당 레이어에 필요한 DI 인터페이스를 자동으로 상속한다 — 사용자는 마커 한 줄만 붙이면 된다.

| 마커 인터페이스 | 의도 | 상속 |
|---|---|---|
| `IDataLayer` | private SubscribableProperty 보유, 외부 노출은 `IReadOnly~`로만 | `IDependencyObject` |
| `IDomainServiceLayer` | 단일 Data 변경 권한 (singleton 권장) | `IDependencyObject`, `IInjectable` |
| `IDomainServiceLayer<TOwnedData>` | 위와 동일 + 변경 권한이 있는 Data를 타입으로 명시 (선택, `[OwnerOnly]`와 함께 사용) | `IDomainServiceLayer` |
| `IApplicationServiceLayer` | 다중 Data/DomainService 조율 (singleton 권장) | `IDependencyObject`, `IInjectable` |
| `IViewModelLayer` | View가 사용할 값/명령 (singleton 또는 per-view) | `IDependencyObject`, `IInjectable` |
| `IViewLayer` | UI/Unit visualization (MonoBehaviour 권장) | `IInjectable` |

> Lifetime은 권장만 하며 강제하지 않는다. AppService/DomainService/Data는 보통 `AsSingleton()` (RootScope), ViewModel은 케이스에 따라 `AsSingleton()` 또는 `AsScoped()`/`AsTransient()`.

---

## 사용 예시

### Data 레이어

```csharp
public interface IPlayerData : IDataLayer
{
    IReadOnlySubscribableProperty<int> Health { get; }
    IReadOnlySubscribableProperty<string> Name { get; }
    void SetHealth(int value);
    void SetName(string value);
}

public class PlayerData : IPlayerData
{
    private readonly SubscribableProperty<int> _health = new(100);
    private readonly SubscribableProperty<string> _name = new("Player");

    public IReadOnlySubscribableProperty<int> Health => _health;
    public IReadOnlySubscribableProperty<string> Name => _name;

    public void SetHealth(int v) => _health.Value = v;
    public void SetName(string v) => _name.Value = v;
}
```

### DomainService 레이어

```csharp
public interface IPlayerDomain : IDomainServiceLayer
{
    void ApplyDamage(int amount);
}

public class PlayerDomain : IPlayerDomain
{
    [Inject] private IPlayerData _player;            // Data ← OK (하위)
    [Inject] private IEnemyData _enemy;              // 다른 Data 참조도 readonly로 가능

    public void ApplyDamage(int amount)
    {
        var defense = _enemy.Defense.Value;          // readonly 읽기
        var final = Math.Max(0, amount - defense);
        _player.SetHealth(_player.Health.Value - final);
    }
}
```

### ApplicationService 레이어

```csharp
public interface ICombatApp : IApplicationServiceLayer
{
    void ResolveAttack(int rawDamage);
}

public class CombatApp : ICombatApp
{
    [Inject] private IPlayerDomain _playerDomain;   // DomainService ← OK
    [Inject] private IScoreData _score;              // Data ← OK (건너뛰기 허용)

    public void ResolveAttack(int raw)
    {
        _playerDomain.ApplyDamage(raw);
        _score.Add(raw);
    }
}
```

### ViewModel 레이어

```csharp
public interface IPlayerVM : IViewModelLayer
{
    IReadOnlySubscribableProperty<int> Health { get; }
    void OnAttackButton();
}

public class PlayerVM : IPlayerVM
{
    [Inject] private IPlayerData _data;             // Data ← OK
    [Inject] private ICombatApp _combat;            // ApplicationService ← OK

    public IReadOnlySubscribableProperty<int> Health => _data.Health;
    public void OnAttackButton() => _combat.ResolveAttack(10);
}
```

### View 레이어 (MonoBehaviour)

```csharp
public class PlayerHUD : DIBehaviour, IViewLayer
{
    [Inject] private IPlayerVM _vm;                  // ViewModel ← OK
    [SerializeField] private TMP_Text _healthText;
    [SerializeField] private Button _attackButton;

    void Start()
    {
        _vm.Health
           .Subscribe(hp => _healthText.text = $"HP: {hp}", invokeInitial: true)
           .AddTo(_cd);

        _attackButton.onClick.AddListener(_vm.OnAttackButton);
    }
}
```

---

## DomainService의 소유권 명시 (선택)

DomainService는 "단일 Data의 변경 책임자"라는 게 컨벤션이지만, 컴파일러는 그걸 모른다. 같은 Data에 다른 DomainService나 ApplicationService가 setter를 직접 호출해도 막을 길이 없으면 layer 분리의 의미가 사라진다.

이를 컴파일 타임에 강제하고 싶다면, **`IDomainServiceLayer<TOwnedData>`로 소유 데이터를 선언**하고 **변경 메서드에 `[OwnerOnly]`를 붙인다**. 인터페이스를 분리할 필요 없이 메서드 단위로 게이트 친다.

```csharp
public class PlayerData : IDataLayer
{
    private readonly SubscribableProperty<int> _health = new(100);
    public IReadOnlySubscribableProperty<int> Health => _health;

    [OwnerOnly] public void SetHealth(int v) => _health.Value = v;
}

public interface IPlayerDomain : IDomainServiceLayer<PlayerData>
{
    void ApplyDamage(int amount);
}

public class PlayerDomain : IPlayerDomain
{
    [Inject] private PlayerData _player;   // owner
    [Inject] private EnemyData  _enemy;    // 다른 Data는 read-only로만

    public void ApplyDamage(int dmg)
    {
        _player.SetHealth(_player.Health.Value - dmg);   // ✅ owner
        // _enemy.SetDefense(0);                          // ❌ KDI003: not owner of EnemyData
    }
}

public class CombatApp : ICombatApp     // IApplicationServiceLayer
{
    [Inject] private PlayerData _player;
    [Inject] private IPlayerDomain _playerDomain;

    public void ResolveAttack(int dmg)
    {
        // _player.SetHealth(0);            // ❌ KDI003: AppService는 어떤 Data의 owner도 아님
        _playerDomain.ApplyDamage(dmg);     // ✅ 도메인을 거쳐서 변경
    }
}
```

규칙 요약:

- `IDomainServiceLayer<T>`는 "이 도메인이 T의 변경 책임자"임을 타입으로 선언한다. 코드 리뷰어가 한눈에 owner를 확인할 수 있다.
- `[OwnerOnly]` 멤버 호출은 owner DomainService만 가능. 그 외 호출은 컴파일 에러(`KDI003`).
- Data 클래스 내부(서브클래스 포함)에서 자기 `[OwnerOnly]` 메서드를 호출하는 건 항상 허용.
- 속성에 `[OwnerOnly]`를 붙이면 양방향 차단. set 접근자에만 붙이면(`{ get; [OwnerOnly] set; }`) 읽기는 허용, 쓰기만 차단.
- 다중 소유가 필요하면 `IDomainServiceLayer<DataA>, IDomainServiceLayer<DataB>` 식으로 여러 번 구현하면 된다.
- 완전 opt-in이다. `[OwnerOnly]`를 안 붙이면 기존과 동일하게 자유롭게 호출 가능 — 중요한 Data부터 점진 적용을 권장한다.

---

## 검증

레이어 방향 위반을 두 단계로 잡는다.

### 1단계 — Roslyn Analyzer (컴파일 타임)

패키지에 동봉된 `Kylin.DI.Layered.Analyzer.dll`이 IDE/컴파일러에 자동으로 등록되어 `[Inject]` 필드의 레이어 방향을 검사한다. 위반은 빨간 밑줄로 보이고 빌드가 실패한다. **별도 설정 없이 동작.**

| 진단 코드 | 의미 | severity |
|---|---|---|
| `KDI001` | 같은 레이어끼리 주입 (ViewModel ↔ ViewModel 등) | Error |
| `KDI002` | 하위 레이어가 상위 레이어를 주입 (ViewModel → View 등) | Error |
| `KDI003` | `[OwnerOnly]` 멤버를 owner가 아닌 클래스에서 호출 | Error |

```csharp
public class BadVM : IPlayerVM
{
    [Inject] private IEnemyVM _enemy;   // KDI001: same-layer
    [Inject] private IPlayerHUD _hud;   // KDI002: upward
}
```

런타임 오버헤드는 0. 분석은 컴파일/IDE 시간에만 동작한다.

### 2단계 — Runtime Validator (선택)

`LayerValidator`로 동적으로 등록되는 타입(reflection으로 늦게 주입되는 케이스 등)도 검사할 수 있다. analyzer가 잡지 못하는 reflection 기반 코드를 보완한다.

```csharp
public class GameScope : LifetimeScope
{
    protected override void Configure(ScopeBuilder builder)
    {
        LayerValidator.Validate(typeof(PlayerData));   // 단일 타입
        LayerValidator.ValidateAssembly(typeof(PlayerData).Assembly);  // 어셈블리 전체

        builder.Bind<IPlayerData>().To<PlayerData>().AsSingleton();
        // ...
    }
}
```

위반 시 `LayerViolationException` 발생.

---

## 무엇을 강제하고 무엇을 강제하지 않는가

**강제**:
- `[Inject]` 필드의 레이어 방향 (같은 레이어 / 상위 레이어 금지) — 항상
- `[OwnerOnly]` 멤버 호출자 (owner DomainService 한정) — 명시적으로 attribute를 붙인 경우에만 opt-in

**강제하지 않음** — 컨벤션으로만 권장:
- Lifetime (AsSingleton/Scoped/Transient): 자유롭게 선택
- 레이어 안에서의 책임 분할 (Data 안에 Setter만 둘지, 검증 로직도 둘지 등)
- 인터페이스 분리 강도
- 한 칸 건너뛰는 의존 (View → DomainService 등 — 허용. 막을 필요 없다고 판단)

엄격함이 늘면 확장성이 줄어든다. 이 패키지는 "휴먼 에러로 가장 자주 발생하는 동일/역방향 참조"만 차단한다.

---

## 의존성

- `com.kylin.di` >= 1.1.2
- `com.kylin.subscribable` >= 1.0.1
