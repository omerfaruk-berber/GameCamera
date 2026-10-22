# Director's Camera Yol Haritası

## Amaç

Sinematik kamera özelliklerini taşıyacak sade ve bağımsız bir gövde oluşturmak.
`DirectorsCamera`, Unity `Camera` bileşeniyle çalışır; `MainCamera`dan türemez.
Kod adları, Inspector etiketleri, yardım metinleri ve tooltip'ler İngilizcedir.
Kullanım rehberi Türkçedir.

## 1. Aşama — Kamera gövdesi

- `Assets/Scripts/DirectorsCamera.cs` aynı nesnede bir `Camera` gerektirir ve
  birden fazla eklenemez.
- `Camera Mode` ile 2D ortografik veya 3D perspektif görünüm seçilir. Gövde ilk
  eklendiğinde 2D başlar ve seçim oyun başlangıcında Unity Camera'ya uygulanır.
- Gövde otomatik takip, dönüş veya boyut güncellemesi yapmaz. Transform sahneden,
  üst nesneden veya başka bir koddan yönetilebilir; başlangıç pozuna geri zorlanmaz.
- Arka plan rengi doğrudan Unity Camera üzerinde saklanır. `Background Color`
  değiştirildiğinde düz renk arka planı etkinleşir. Ayrı renk kopyası tutulmaz.
- `Assets/Editor/DirectorsCameraEditor.cs`, arka plan rengini ve `Camera Features`
  alanını gösterir. Dolly Camera bu alandaki `Add Feature` menüsünden eklenebilir.

## Kurulum

1. Ayrı bir kamera nesnesi oluştur veya `MainCamera` içermeyen bir kamera seç.
2. `Add Component > Camera > Director's Camera` ile gövdeyi ekle. Camera bileşeni
   yoksa Unity otomatik ekler.
3. Transform üzerinden konum ve dönüşü ayarla. Örneğin standart XY düzlemindeki
   bir 2B sahne için Z konumu `-10`, dönüş `(0, 0, 0)` olabilir.
4. Director's Camera üzerindeki `Camera Mode` alanından `2D` veya `3D` seç.
   2D görüşü `Size`, 3D görüşü `Field of View` ile ayarla.
5. `Director's Camera > Background Settings > Background Color` üzerinden rengi seç.
   Renk düzenlemeleri Undo/Redo ve prefab değişiklik kaydını destekler.
6. Görüntü verecek Camera ve AudioListener bileşenlerini sahnendeki ihtiyaca göre
   kendin etkinleştir. DirectorsCamera'nın Enabled kutusu render geçişi yapmaz.

Bu aşama sahneye kendiliğinden ikinci kamera eklemez; kamera geçişleri, render
önceliği, AudioListener yönetimi ve Timeline bağlantısı içermez. Mevcut MainCamera
özellikleri kendi gövdelerine bağlıdır ve Director's Camera menüsüne eklenmez.

## Koddan kullanım

| API | Davranış |
| --- | --- |
| `ControlledCamera` | Aynı nesnedeki Unity Camera bileşenini döndürür; referans salt okunurdur. |
| `BackgroundColor` | Unity Camera'nın gerçek rengini okur/yazar; yazıldığında `SolidColor` seçilir. |
| `SetPose(Vector3 position, Quaternion rotation)` | Dünya koordinatlarında konumu ve dönüşü anında uygular. Z dahil üç eksen serbesttir. |
| `SetCameraMode(GameCameraMode mode)` | 2D ortografik veya 3D perspektif davranışa geçer. |

```csharp
// directorCamera, Inspector üzerinden bağlanan bir DirectorsCamera referansıdır.
directorCamera.BackgroundColor = Color.black;

// Kamerayı dünya koordinatlarında bir çekim noktasına yerleştir.
directorCamera.SetPose(shotPoint.position, shotPoint.rotation);

// 3D perspektif moda geç ve görüş açısını ayarla.
directorCamera.SetCameraMode(GameCameraMode.Mode3D);
directorCamera.ControlledCamera.fieldOfView = 50f;
```

`SetPose` çağrısı yumuşatma veya zaman adımı kullanmaz; oyun duraklatılmışken de
açıkça çağrıldığında uygulanır. Bu API kendiliğinden hareket başlatmaz.
2D modunda `FollowTrack` dönüşleri kamera düzleminde kalır ve Rotation Offset'in
yalnız Z bileşeni kullanılır. `LookAtTarget` yalnızca 3D modunda geçerlidir.
3D modunda ray yönü ve hedef bakışı üç eksende hesaplanır.

## 2. Aşama — Dolly Camera (eklendi)

Dolly sistemi birbirinden bağımsız iki parçadan oluşur:

- `CameraDollyTrack.cs`: kontrol noktalarını, ray şeklini ve Scene önizlemesini taşır.
- `CameraDollyCart.cs`: Director's Camera'yı ray üzerinde oynatır; hız, tekrar ve
  dönüş davranışını yönetir.

### Kurulum

1. Director's Camera Inspector'ında `Add Feature > Dolly Camera (Track & Cart)` seç.
2. Açılan özellik panelinde `Create Dolly Track` düğmesine bas. Sahne kökünde
   `Dolly Track` ve altında iki kontrol noktası oluşturulur; ray cart'a otomatik bağlanır.
3. Hierarchy'deki `Dolly Point` nesnelerini Scene penceresinde taşıyarak rotayı çiz.
   Daha fazla nokta için mevcut bir noktayı çoğaltıp `Control Points` listesine ekle.
4. Ray nesnesinde `Interpolation` ile düz veya yumuşak yolu, `Closed Loop` ile son
   noktanın ilk noktaya bağlanmasını seç.
5. Director's Camera panelinde hızı, başlangıç konumunu, oynatma ve dönüş ayarlarını yap.
6. Play Mode sırasında `Play`, `Pause` ve `Stop` düğmeleriyle sonucu dene.

Track ayrı bir sahne nesnesidir ve kameranın altına yerleştirilmez; böylece kamera
hareket ederken ray da onunla birlikte sürüklenmez. Cart özelliğini kaldırmak ayrı
ray nesnesini silmez. Aynı ray daha sonra başka bir Director's Camera'ya atanabilir.

### Dolly Track alanları

| Inspector alanı | Davranış |
| --- | --- |
| `Control Points` | Rayı sırasıyla oluşturan Transform listesi; boş referanslar atlanır. |
| `Interpolation` | `Linear` düz parçalar, `Smooth` Catmull-Rom eğrisi kullanır. |
| `Closed Loop` | Son noktayı ilk noktaya bağlar. |
| `Samples Per Segment` | Yumuşak eğride uzunluk ve sabit hız yaklaşımının hassasiyeti. |
| `Show Scene Preview` | Camgöbeği rayı ve turuncu kontrol noktalarını Scene'de gösterir. |

Smooth ray kontrol noktalarından geçer. Eğri uzunluğu örneklenerek hesaplanır;
`Samples Per Segment` yükseldikçe hızın dünya birimlerine yaklaşımı daha hassas olur.
Kontrol noktaları oyun sırasında hareket ettirilirse ray önbelleği otomatik yenilenir.

### Dolly Cart alanları

| Inspector alanı | Davranış |
| --- | --- |
| `Track` | Kullanılacak CameraDollyTrack referansı. |
| `Speed` | Saniye başına yaklaşık dünya birimi. |
| `Start Position` | Ray üzerindeki 0–1 başlangıç konumu. |
| `Play On Start` | Oyun başladığında otomatik oynatır. |
| `Playback Mode` | `Once`, `Loop` veya iki uç arasında `PingPong`. |
| `Orientation` | Mevcut dönüşü korur, ray yönünü izler veya bir hedefe bakar. |
| `Look At Target` | Yalnız 3D modundaki `LookAtTarget` dönüşünde kullanılan hedef. |
| `Rotation Offset` | Hesaplanan dönüşe eklenen Euler açıları. |
| `Rotation Damping` | Dönüş yumuşama süresi; `0` anında döner. |
| `Show Scene Preview` | Başlangıç/cart konumunu ve ray yönünü Scene'de gösterir. |

`KeepRotation` varsayılandır ve düz 2B kameranın açısını korur. `FollowTrack`,
kamera ileri eksenini hareket yönüne çevirir; bu ayar 3B sinematik hareketler için
uygundur. `LookAtTarget` yalnızca 3D modunda kamerayı atanmış hedefe yöneltir.
2D modunda bu seçenek mevcut dönüşü değiştirmez. Cart konumu her karede
`DirectorsCamera.SetPose` üzerinden uygulanır. `Time.timeScale = 0` olduğunda otomatik
ilerleme durur; açıkça çağrılan `Stop` ve konumlandırma metotları çalışmaya devam eder.
Kapalı bir rotada kesintisiz tekrar için Track'teki `Closed Loop` ile Cart'taki
`Loop` ayarlarını birlikte kullan.

### Koddan kullanım

```csharp
// Rayı bağla ve başlangıçtan oynat.
dollyCart.SetTrack(dollyTrack);
dollyCart.Stop();
dollyCart.Play();

// Mevcut konumda duraklat veya rayın ortasına anında git.
dollyCart.Pause();
dollyCart.SetNormalizedPosition(0.5f);

// Ray üzerindeki konum ve yönü başka sistemlerde örnekle.
Vector3 position = dollyTrack.EvaluatePosition(0.25f);
Vector3 direction = dollyTrack.EvaluateTangent(0.25f);
```

Sistem yeni bir paket veya Cinemachine bağımlılığı kullanmaz. Kamera geçişi,
Timeline bağlantısı ve birden fazla çekimi sıralama daha sonraki aşamalara bırakılır.

## Doğrulama ve kabul kontrolleri

- Yeni runtime/editor dosyalarının Unity proje derlemesine dahil olduğunu doğrula.
- Bileşeni eklemenin Camera gereksinimini karşıladığını, ikinci kopyaya izin
  vermediğini ve konum/dönüş/boyutu değiştirmediğini kontrol et.
- Perspektif seçip Play Mode'a gir; seçimin, konumun ve dönüşün korunduğunu doğrula.
  Açma/kapama ve ortografik görünüm için aynı kontrolü tekrarla.
- `SetPose` ile X/Y/Z ve dönüşü, döndürülmüş bir üst nesne altında da kontrol et.
- Inspector'dan renk değiştir; Unity Camera alanı ve görüntüyle eşleştiğini,
  Undo/Redo ile rengin ve önceki arka plan modunun geri alınabildiğini doğrula.
- Prefab örneğinde renk düzenlemesini kaydet ve yeniden açarak kalıcılığı kontrol et.
- `Add Feature` menüsünün Dolly Camera'yı eklediğini, tekrar eklemeyi engellediğini
  ve `Remove` işleminin ayrı Dolly Track nesnesini koruduğunu kontrol et.
- Linear ve Smooth raylarda başlangıç/bitiş konumlarını, hareket hızını ve kontrol
  noktası taşındığında Scene önizlemesinin yenilenmesini kontrol et.
- Once modunun sonda durduğunu, Loop modunun başa sardığını ve PingPong modunun
  iki uç arasında yön değiştirdiğini doğrula.
- Keep Rotation, Follow Track ve Look At Target dönüşlerini hem ortografik hem
  perspektif kamerada kontrol et.
- Play/Pause/Stop ile `SetNormalizedPosition` çağrılarını ve duraklatmayı doğrula.

Derleme kontrolü, Unity içindeki görsel ve yaşam döngüsü kontrollerinin yerine
geçmez. Çalıştırılamayan kontroller teslim sırasında ayrıca belirtilmelidir.
