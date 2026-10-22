# Director's Camera Yol Haritası

## Amaç

Sinematik kamera özelliklerini taşıyacak sade ve bağımsız bir gövde oluşturmak.
`DirectorsCamera`, Unity `Camera` bileşeniyle çalışır; `MainCamera`dan türemez.
Kod adları ve Inspector etiketleri İngilizce, açıklamalar ve tooltip'ler Türkçedir.

## 1. Aşama — Kamera gövdesi

- `Assets/Scripts/DirectorsCamera.cs` aynı nesnede bir `Camera` gerektirir ve
  birden fazla eklenemez.
- Editörde ilk eklemede veya bileşenin `Reset` komutunda ortografik görünüm seçilir.
  Daha sonra Unity Camera üzerinde seçilen perspektif/ortografik ayarı korunur;
  oyun başlangıcında veya bileşeni açıp kapatınca değiştirilmez.
- Gövde otomatik takip, dönüş veya boyut güncellemesi yapmaz. Transform sahneden,
  üst nesneden veya başka bir koddan yönetilebilir; başlangıç pozuna geri zorlanmaz.
- Arka plan rengi doğrudan Unity Camera üzerinde saklanır. `Background Color`
  değiştirildiğinde düz renk arka planı etkinleşir. Ayrı renk kopyası tutulmaz.
- `Assets/Editor/DirectorsCameraEditor.cs`, arka plan rengini ve ileride kullanılacak
  `Camera Features` alanını gösterir. Henüz özellik bulunmadığı için `Add Feature`
  pasiftir ve `No features available yet.` açıklaması görünür.

## Kurulum

1. Ayrı bir kamera nesnesi oluştur veya `MainCamera` içermeyen bir kamera seç.
2. `Add Component > Camera > Director's Camera` ile gövdeyi ekle. Camera bileşeni
   yoksa Unity otomatik ekler.
3. Transform üzerinden konum ve dönüşü ayarla. Örneğin standart XY düzlemindeki
   bir 2B sahne için Z konumu `-10`, dönüş `(0, 0, 0)` olabilir.
4. Unity Camera üzerinden `Projection` seçimini yap: 2B için `Orthographic`,
   perspektif görünüm için `Perspective`. Görüşü `Size` veya `Field of View` ile ayarla.
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

```csharp
// directorCamera, Inspector üzerinden bağlanan bir DirectorsCamera referansıdır.
directorCamera.BackgroundColor = Color.black;

// Kamerayı dünya koordinatlarında bir çekim noktasına yerleştir.
directorCamera.SetPose(shotPoint.position, shotPoint.rotation);

// Görüş ayarlarına Unity Camera üzerinden eriş.
directorCamera.ControlledCamera.orthographic = false;
directorCamera.ControlledCamera.fieldOfView = 50f;
```

`SetPose` çağrısı yumuşatma veya zaman adımı kullanmaz; oyun duraklatılmışken de
açıkça çağrıldığında uygulanır. Bu API kendiliğinden hareket başlatmaz.

## 2. Aşama — Dolly Camera (sonra)

Ray, kontrol noktaları, cart hareketi ve oynatma kontrolleri ayrı özellik
dosyalarında geliştirilecek. Dolly özelliği Director's Camera üzerindeki
`Add Feature` menüsüne ve düzenlenebilir özellik paneline bağlanacak.
Konum ve dönüş için gövdenin `SetPose` uygulama noktası kullanılabilecek.
Ray türü, hız ve oynatma davranışları bu aşama planlanırken belirlenecek.
Temel gövde yeni bir paket veya Cinemachine bağımlılığı gerektirmez.

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
- `Add Feature` düğmesinin pasif, açıklamasının görünür olduğunu kontrol et.

Derleme kontrolü, Unity içindeki görsel ve yaşam döngüsü kontrollerinin yerine
geçmez. Çalıştırılamayan kontroller teslim sırasında ayrıca belirtilmelidir.
