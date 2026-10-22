# 2B Kamera Yol Haritası

## Amaç

Önce sade ve kullanılabilir bir kamera gövdesi oluşturmak, sonra farklı oyun
türlerinin ihtiyaçlarını ayrı C# dosyalarıyla eklemek. Kod, alan ve metot adları
İngilizce; açıklamalar ve kullanım rehberi Türkçe olacak.

## 1. Aşama — Ana gövde (bu sürüm)

- Mevcut `Assets/Scripts/MainCamera.cs` dosyasını temel kamera bileşeni yapmak.
- Kamerayı ortografik (perspektifsiz 2B görünüm) kullanmak.
- Bir `Transform` hedefini X/Y düzleminde `LateUpdate` içinde takip etmek.
- `offset` ile hedefe göre kadrajı, `smoothTime` ile takip yumuşaklığını ayarlamak.
- Kameranın mevcut dünya Z konumunu korumak; hedefin Z hareketini izlememek.
- Başlangıçta isteğe bağlı olarak hedefe anında yerleşmek.
- Geçerli bir grup veya tek hedef yoksa kamerayı bulunduğu yerde tutmak.
- Başka kodların `SetTarget` ve `SnapToTarget` ile kamerayı yönlendirebilmesi.

Bu gövde standart XY düzlemindeki 2B kamera içindir. Varsayılan dönüş `(0, 0, 0)`;
isteğe bağlı `CameraLookAtTarget2D` bileşeni kamerayı hedefe doğru döndürebilir.
Takip hedefi kameradan bağımsız olmalı; kamera hedefin alt nesnesi yapılmamalı.

## 2. Aşama — Oyun türüne göre takip (sonra)

İhtiyaç seçildikçe ayrı dosyalar ekle:

- `PlatformerCamera2D.cs`: yatay ağırlıklı takip ve hareket yönüne doğru bakış.
- `TopDownCamera2D.cs`: üstten görünüm için kadraj davranışı.
- Dead Zone / Soft Zone davranışı `CameraDeadSoftZone2D.cs` ile eklendi (aşağıda).
- Çoklu hedef takibi `CameraTargetGroup2D.cs` ile eklendi (aşağıda).

## 3. Aşama — Ek davranışlar (sonra)

- `CameraBounds2D.cs`: kamera görüntüsünü bölüm sınırları içinde tutma.
- `CameraZoom2D.cs`: ortografik boyutu değiştirerek yakınlaşma ve uzaklaşma.
- `CameraShake2D.cs`: darbe gibi olaylarda geçici kamera sarsıntısı.

Yeni davranışlar eklenirken aynı karede birden fazla bileşenin kamera konumunu
doğrudan değiştirmesi önlenecek. Gerektiğinde gövdeye küçük bir ortak uygulama
noktası eklenecek; takip, sınırlandırma ve görsel efekt sırası oradan yönetilecek.
Bu aşamada boş altyapı veya ek bağımlılık eklenmeyecek.

## Kurulum

1. `MainCamera` bileşenini sahnedeki `Main Camera` nesnesine ekle.
2. Kameranın dönüşünü `(0, 0, 0)`, Z konumunu örneğin `-10` yap.
3. Takip edilecek karakteri veya nesneyi `Target` alanına sürükle.
4. `Offset` ile X/Y kadraj kaymasını ayarla; örneğin `(0, 1)` hedefin üstünü gösterir.
5. `Smooth Time` için `0.2` ile başla. Daha yüksek değer daha gecikmeli takip,
   `0` ise anında takip sağlar. Süre saniye cinsinden yaklaşık tepki süresidir.
6. `Snap On Start` açıksa kamera oyun başlarken doğrudan hedefe yerleşir.
7. Görüş alanını Camera bileşenindeki `Size` üzerinden ayarla.
8. Arka plan rengini MainCamera üzerindeki `Background Settings > Background Color` alanından ayarla.

Kamera Play sırasında ortografik moda geçer. Hedef `Rigidbody2D` ile hareket
ediyorsa ve fizik adımları görünür titreme oluşturuyorsa hedefin `Interpolation`
ayarını `Interpolate` olarak deneyebilirsin. Kamera oyun zamanını kullanır;
`Time.timeScale = 0` olduğunda otomatik takip durur.

## Başka bir koddan kullanım

```csharp
// Yeni hedefe yumuşak geçiş yap.
cameraController.SetTarget(playerTransform);

// Yeniden doğma veya ışınlanma sonrasında yeni hedefe anında yerleş.
cameraController.SetTarget(respawnedPlayerTransform, snapImmediately: true);

// Mevcut hedefe anında yerleş; önceki takip hızını sıfırla.
cameraController.SnapToTarget();

// Tek hedef takibini bırak; etkin hedef grubu varsa grup takibi sürer.
cameraController.SetTarget(null);

// Kamera arka planını siyah yap; düz renk arka plan modunu etkinleştir.
cameraController.BackgroundColor = Color.black;
```

Burada `cameraController`, Inspector üzerinden bağlanan bir `MainCamera`
referansıdır. `Target` özelliği mevcut hedefi dışarıdan okumak için kullanılabilir.

## Arka plan rengi (eklendi)

Arka plan rengi doğrudan `MainCamera.BackgroundColor` özelliğiyle yönetilir;
ek bir davranış bileşeni eklemek gerekmez. MainCamera Inspector'ındaki
`Background Settings > Background Color` alanından seçilebilir.

Bu alan aynı nesnedeki Unity Camera bileşeninin gerçek `backgroundColor`
değerini gösterir. Renk değiştirildiğinde `clearFlags`, `SolidColor` yapılır;
böylece seçilen renk kameranın arka planını doldurur. Ayar editörde anında
uygulanır; kaydetme, prefab değişiklikleri ve Ctrl+Z Camera bileşeni üzerinden
yönetilir. Unity Camera'nın kendi renk alanından yapılan değişiklikler de
MainCamera panelinde görünür.

## Yönlere göre görüş genişletme (eklendi)

`CameraViewExpansion2D` bileşenini `MainCamera` ile aynı kamera nesnesine ekle.
Ayarlar Play sırasında uygulanır ve dünya birimi cinsindendir:

| Inspector alanı | Davranış |
| --- | --- |
| `Left Expansion` | Sol tarafa ek görüş alanı açar. Sağ kenarı korur. |
| `Right Expansion` | Sağ tarafa ek görüş alanı açar. Sol kenarı korur. |
| `All Sides Expansion` | Üst ve alta belirtilen mesafeyi ekler; sağ ve solu görüntü oranına göre genişletir. |

Sağ ve sol değerleri bağımsızdır ve birlikte kullanılabilir. Üç alanın etkisi
toplanır. Hepsi `0` olduğunda başlangıçtaki boyut ve kadraja dönülür.
`All Sides Expansion` da kullanılıyorsa karşı yatay kenar yalnızca bu ortak
genişlemenin gerektirdiği kadar değişir.

Örnekler:

- Yalnız sağa: `Left Expansion = 0`, `Right Expansion = 4`, `All Sides Expansion = 0`.
- Yalnız sola: `Left Expansion = 3`, `Right Expansion = 0`, `All Sides Expansion = 0`.
- İki tarafa farklı miktarda: `Left Expansion = 2`, `Right Expansion = 5`.
- Her yöne: `Left Expansion = 0`, `Right Expansion = 0`, `All Sides Expansion = 2`.

Standart ortografik kameranın görüntü oranı korunur: sağa veya sola genişletmek
üst ve alt görüş alanını da artırır. Görünen nesneler ekranda daha küçük görünür;
bu işlem yakınlaştırma değil, daha fazla alan gösteren uzaklaştırmadır.
Takip sırasında karaktere göre sağ/sol kadraj korunur; hedef yokken de çalışır.

View Expansion ve Target Group Camera ortak bir başlangıç boyutu kullanır.
İlk etkinleşen özellik Camera üzerindeki `Size` değerini kaydeder; diğeri aynı
değeri paylaşır. `MainCamera`, genişlemeyi temel boyuta ekleyerek uygular; geçerli
bir hedef grubu varsa temel boyut grup kadrajından gelir. Özelliklerin eklenme
veya açılma sırası boyutu biriktirmez.

Başlangıç boyutunu elle değiştirmek için **hem View Expansion hem Target Group
Camera özelliklerini kapat**, Camera `Size` değerini ayarla ve özellikleri yeniden
aç. İlk açılan özellik yeni başlangıç boyutunu kaydeder. Son boyut özelliği de
kapatıldığında bu başlangıç değeri geri yüklenir. View Expansion kapatıldığında
kendi genişlemesi ve kadraj kayması geri alınır; etkin grup kadrajı devam eder.
`MainCamera.SetViewOffset` ek davranışlar için tek kadraj uygulama noktasıdır;
bu sürümde bu noktayı yalnızca `CameraViewExpansion2D` yönetir.

```csharp
// Yalnız sağa 4 dünya birimi ek görüş alanı aç.
viewExpansion.SetExpansion(left: 0f, right: 4f);

// Sağ ve solu birbirinden bağımsız ayarla.
viewExpansion.SetExpansion(left: 2f, right: 5f);

// Her yöne genişlet.
viewExpansion.SetExpansion(left: 0f, right: 0f, allSides: 2f);

// Başlangıç görünümüne dön.
viewExpansion.SetExpansion(left: 0f, right: 0f);
```

`viewExpansion`, bir `CameraViewExpansion2D` referansıdır.

## Scene üzerinde genişleme önizlemesi (eklendi)

Scene penceresinde `Gizmos` açıkken `CameraViewExpansion2D` şu çizimleri gösterir:

- Camgöbeği çerçeve (`Base View`): genişletmeden önceki kadraj.
- Yeşil çerçeve (`Expanded View`): genişletme ayarlarıyla oluşacak kadraj.
- Saydam yeşil alanlar: sağa, sola, üste ve alta eklenen görüş alanı.

Oynatmaya basmadan Inspector'daki genişleme değerlerini değiştirerek sonucu
görebilirsin. Çizimler Play sırasında da kamerayla birlikte hareket eder.
`Show Scene Preview` ile kapatılabilir. Kamera nesnesini seçmek gerekmez;
Gizmos menüsünde bu bileşenin çizimlerinin de açık olması gerekir.
Çizimler kamera düzlemiyle hizalanır; merkez derinliği standart 2B oyun
düzleminden (`Z = 0`) alınır. Camera ayarlarını veya nesnelerin konumlarını
değiştirmez. Önizleme, Camera'nın görüntü oranını kullanır.

## Look At Target (eklendi)

`CameraLookAtTarget2D.cs` bileşenini aynı kamera nesnesine ekle:

- `Look At Target`: kameranın bakacağı hedef. Boşsa geçerli hedef grubunun merkezi,
  grup yoksa `MainCamera.Target` kullanılır.
- `Rotation Damping`: dönüş yumuşama süresi; `0` hedefe anında döner.
- `Restore Rotation On Disable`: bileşen kapatılınca önceki dönüşü geri yükler.
- `Show Scene Preview`: turuncu hedef çizgisini ve hedef işaretini gösterir.

Bu bileşen gerçek bir bakış dönüşü uygular: kameranın ileri ekseni hedefe yönelir.
Kamera konumunu ve Z değerini değiştirmez. 2B sprite düzlemine eğik bakıldığında
görüntü eğilir/sıkışır; bu etkiyi istemediğinde bileşeni kapatabilirsin.
Yumuşak dönüşte hareket eden hedefe bakış zamanla yaklaşır; tam ve anlık yönelme
için `Rotation Damping = 0` kullan. Hedef yoksa veya kamera ile aynı konumdaysa
dönüş değiştirilmez. Tek hedef takibinde çok büyük hareketler ve yumuşak dönüşte
hedefin her karede kadraj içinde kalması garanti edilmez; Target Group Camera
etkinse grup üyeleri için ek boyut hesabı uygulanır.

`SetLookAtTarget(newTarget, snapImmediately: true)` yeni hedefe anında döner.
`null` verilmesi grup merkezine veya takip hedefine geri döner; dönüşü durdurmak için bileşeni kapat.
`MainCamera` hedefi boş olsa da özel bir bakış hedefiyle dönüş çalışır.

## Dead Zone & Soft Zone (eklendi)

`CameraDeadSoftZone2D.cs` bileşenini aynı kamera nesnesine ekle.
Takip edilecek karakter `MainCamera.Target` alanından alınır; Target Group Camera
etkin ve grupta geçerli hedef varsa aynı bölgeler grup merkezine uygulanır.

| Alan | Anlamı |
| --- | --- |
| `Dead Zone Size` | Kameranın yer değiştirmediği iç alanın X/Y boyutu. |
| `Soft Zone Size` | Yumuşak takip alanının dış sınırının X/Y boyutu. |
| `Damping` | İç sınıra yakın takip yumuşama süresi. `0` anında tepki verir. |
| `Show Scene Preview` | Kırmızı Dead Zone ve sarı Soft Zone çerçevelerini gösterir. |

Boyutlar kadraja oranla `0–1` arasındadır: `(0.2, 0.2)` genişlik ve yüksekliğin
yüzde 20'sidir. Her iki bölge kadrajın merkezindedir. Soft Zone, Dead Zone'dan
küçük verilemez; değerler otomatik sınırlandırılır. `SetZoneSizes(deadSize, softSize)`
ile koddan da ayarlanabilir. Ekran oranı ve görüş genişletmesi değiştiğinde
bölgeler yeni kadraj boyutuna uyum sağlar.

Hedef iç alandayken kamera konumu sabit kalır. İç alanın dışına çıkınca en yakın
iç kenara doğru takip başlar; dış sınıra yaklaştıkça tepki hızlanır. Hedef çok
hızlı hareket eder veya ışınlanırsa dış sınırı korumak için anlık konum düzeltmesi
yapılabilir. X ve Y ayrı hesaplanır; Z korunur. Bileşen açıkken takip yumuşamasını
bu bileşenin `Damping` alanı yönetir; `MainCamera.Smooth Time` ikinci kez uygulanmaz.
Kapatıldığında standart takip geri gelir. `SnapToTarget` bilinçli bir anında
yerleşme çağrısıdır ve bölgeleri atlar.

Kadraj referansı, takip hedefinin konumuna `MainCamera.Offset` ve görüş
genişletmesinin kayması eklenerek hesaplanır. Offset kullanıyorsan bölgeler
karakterin kendisine değil bu kaydırılmış referans noktasına uygulanır.

## Target Group Camera (eklendi)

`MainCamera > Add Feature > Target Group Camera` ile ayrı
`CameraTargetGroup2D.cs` bileşenini ekle. `Targets` listesine iki dövüşçü, co-op
oyuncuları veya aynı kadrajda tutulacak diğer nesneleri sürükle.

| Alan | Anlamı |
| --- | --- |
| `Targets` | Birlikte takip edilecek `Transform` listesi. |
| `Include Renderer Bounds` | Varsayılan açık. Etkin Renderer sınırlarını kadraja dahil eder; uygun Renderer yoksa hedefin konumunu kullanır. |
| `Padding` | Hedef sınırlarına eklenecek X/Y boşluğu; dünya birimi cinsindendir. |
| `Minimum Size` | En küçük ortografik boyut. Varsayılan `5`. |
| `Zoom Damping` | Yakınlaşmanın yumuşama süresi. Varsayılan `0.35` saniye; `0` anında yakınlaşır. |
| `Rotate To Group` | İsteğe bağlı olarak grup merkezine döner. Varsayılan kapalı. |
| `Rotation Damping` | Grup dönüşünün yumuşama süresi. Varsayılan `0.15` saniye. |
| `Show Scene Preview` | Scene üzerinde grup sınırlarını ve üyeleri gösterir. Gizmos açık olmalıdır. |

Kamera grup sınırlarının merkezini mevcut `Smooth Time` ile takip eder; Dead Zone
& Soft Zone açıksa konumu bu bölgeler yönetir. Kamera Z konumunu korur. Ortografik
2B kamerada uzaklaşma, kamerayı Z boyunca taşımak yerine `Size` artırılarak yapılır.
Gerekli boyut ekran oranına, kameranın gerçek yumuşatılmış konumuna ve son dönüşüne
göre hesaplanır. Hedefler ayrıldığında kadrajı korumak için hemen uzaklaşır;
yaklaştıklarında `Zoom Damping` ile yumuşak yakınlaşır. Sert bir maksimum boyut yoktur.

Boş, silinmiş ve hiyerarşide etkin olmayan hedefler atlanır. Grupta geçerli hedef
kalmadığında veya özellik kapatıldığında normal `MainCamera.Target` takibi ve
özelliklerin paylaştığı başlangıç boyutu geri gelir. View Expansion açıksa genişleme bu
temel boyuta eklenmeye devam eder. Etkin Look At bileşeni dönüşte önceliklidir;
onun özel hedefi boşsa grup merkezini kullanır. Look At kapalıyken `Rotate To Group`
ile gruba dönülebilir. Varsayılan kapalı ayar düz 2B görünümü korur.

```csharp
// Gruba hedef ekle; zaten varsa tekrar ekleme.
targetGroup.AddTarget(playerTransform);

// Bir hedefi takip grubundan çıkar.
targetGroup.RemoveTarget(playerTransform);

// Grubun geçerli hedeflerini kapsayan dünya sınırlarını oku.
if (targetGroup.TryGetGroupBounds(out Bounds groupBounds))
{
    Vector3 groupCenter = groupBounds.center;
}

// Bütün hedefleri çıkar; normal tek hedef takibine dön.
targetGroup.ClearTargets();
```

`targetGroup`, bir `CameraTargetGroup2D` referansıdır. Sahne çizimleri grup
sınırlarını ve geçerli üyeleri gösterir; `Show Scene Preview` ile kapatılabilir.

## Birlikte çalışma sırası

`MainCamera` geçerli grup varsa grup merkezini, yoksa normal hedefi seçer. Önce
normal takibi veya bölge takibini, ardından Look At ya da isteğe bağlı grup
dönüşünü uygular. Grup kadrajı bu gerçek konum ve dönüşle hesaplanır; View Expansion
grup boyutunun üzerine eklenir. Konum, dönüş ve son boyut merkezi olarak uygulanır;
ek bileşenler bağımsız `LateUpdate` metotlarıyla kamera için yarışmaz. Ana bileşen
açık olmalıdır. Hedef silinmesi ve oyun duraklatılması kontrol edilir; otomatik
takip ve grup güncellemeleri `Time.timeScale = 0` iken durur.

Look At ile bölgeler aynı anda açıksa bölgeler konum hareketini, Look At ise
dönüşü yönetir. Aynı hedefe bakılıyorsa hedef dönüşle merkezde tutulabilir ve
konum takibine daha az ihtiyaç kalabilir. Dönüş, hedefin ekrandaki konumunu
değiştirdiğinden bölge sınırları dönüşten önceki konum hesabına uygulanır.
Sabit bir 2B kadrajda Dead Zone davranışını görmek için Look At'ı kapalı tut.
Kamera oyun düzlemine neredeyse paralel bakarsa Z sabit tutularak kadraj kayması
hesaplanamaz; bölge ve yönlü genişleme kayması bu özel durumda sıfırlanır.

## MainCamera Inspector üzerinden özellik yönetimi (eklendi)

Kamera nesnesini seçip `MainCamera` bileşenindeki `Add Feature` düğmesine bas:

- `View Expansion`: görüş genişletme bileşenini ekler.
- `Look At Target`: bakış hedefine dönme bileşenini ekler.
- `Dead Zone & Soft Zone`: takip bölgeleri bileşenini ekler.
- `Target Group Camera`: çoklu hedef takibi ve otomatik kadraj bileşenini ekler.

Eklenen özelliklerin ayarları MainCamera Inspector'ının altında açılır panellerde
gösterilir. `Enabled` ile açılıp kapatılır, `Remove` ile silinir.
Ekleme, kaldırma, açma/kapama ve ayar değişiklikleri Unity Undo sistemiyle geri
alınabilir. Zaten ekli özellikler menüde işaretlenir ve tekrar eklenemez.
Önceden normal `Add Component` menüsüyle eklenen özellikler de otomatik tanınır.

Her özellik aynı kamera nesnesinde ayrı bir bileşen olarak kalır; MainCamera
paneli bu bileşenin kendi verilerini düzenler. Bileşenin normal Inspector paneli
de kullanılabilir ve iki yerdeki ayarlar aynı veriyi gösterir.
`Assets/Editor/MainCameraEditor.cs` yalnızca Unity editöründe çalışır; oyun
derlemesine dahil edilmez. Yeni bir özelliği bu menüye eklemek için bu dosyadaki
menü ve panel listesine ilgili bileşen eklenir.

## Doğrulama

Beş kamera dosyası hem `UNITY_EDITOR` açıkken hem kapalıyken Unity referanslarıyla
derlenerek kontrol edilir. Geçici kontrol programı, bölge hesaplarının iç alanda hareketsiz kalmasını,
simetriyi, hedefi aşmamayı, dış sınırı korumayı, duraklatmayı, sıfır yumuşatmayı,
eşit bölge sınırlarını ve dönmüş kamerada Z sabit konum dönüşümünü kontrol eder.
Grup kadrajında farklı ekran oranları, gerçek takip konumu/dönüşü, boş ve silinmiş
hedefler, Renderer sınırları, hızlı uzaklaşma, yumuşak yakınlaşma, özelliklerin
birlikte çalışması ve boyutun geri yüklenmesi ayrıca doğrulanır.
Unity Scene / Play Mode üzerinde görsel kontrol ayrıca yapılmalıdır.

## Unity içinde kabul kontrolü

- Hedef hareket ettiğinde kamera onu yumuşak takip etmeli; Z konumu değişmemeli.
- `Smooth Time = 0` olduğunda kamera hedefe ve offset'e doğrudan yerleşmeli.
- Hedef boşken veya oyun sırasında silindiğinde hata oluşmamalı.
- `Snap On Start` açık/kapalı durumları başlangıç geçişini değiştirmeli.
- Hedef değiştirme, takibi bırakma ve anında yerleşme çağrıları çalışmalı.
- Oyun duraklatıldığında otomatik takip durmalı ve konum bozulmamalı.
- Yalnız sağa/sola genişletmede karşı yatay kenar sabit kalmalı.
- Sağ ve sol miktarları ayrı ayrı ve birlikte çalışmalı; sıfırlama eski kadraja dönmeli.
- Genişleme hedef olmadan ve ekran oranı değiştiğinde de çalışmalı.
- Genişleme bileşenini kapatıp açmak boyutu veya kadrajı biriktirmemeli.
- Edit modunda genişleme değerleri değişince Scene çerçeveleri güncellenmeli.
- Play sırasında yeşil çerçeve kameranın gerçek görüş alanıyla eşleşmeli.
- `Show Scene Preview` kapalıyken çizimler kaybolmalı.
- Look At hedefi hareket ettiğinde kamera yumuşak dönmeli; `Rotation Damping = 0` anında dönmeli.
- Özel bakış hedefi atanmamışken geçerli grup merkezi, grup yoksa MainCamera takip hedefi kullanılmalı.
- Dead Zone içindeyken konum sabit kalmalı; Soft Zone dışına taşmada konum düzeltilmeli.
- Bölgeler kapatılınca normal takip, Look At kapatılınca seçilen dönüş geri yükleme davranışı çalışmalı.
- Görüş genişletmesi ve dönen kamera ile Scene çizimleri kadrajla hizalı kalmalı.
- MainCamera üzerindeki `Add Feature` menüsü dört özelliği ekleyebilmeli; tekrarını engellemeli.
- Özellik panelleri aynı bileşenin ayarlarını değiştirmeli; açma/kapama ve kaldırma çalışmalı.
- Ekleme, kaldırma ve ayar değişiklikleri Ctrl+Z ile geri alınabilmeli.
- MainCamera Background Color değiştiğinde Camera rengi ve düz renk modu güncellenmeli.
- Renk değişikliği Ctrl+Z ile geri alınmalı ve sahne/prefab kaydında korunmalı.
- Gruptaki hedefler farklı yönlere giderken tamamı yatay/dikey ekran oranlarında kadrajda kalmalı.
- Grup merkezini takip ederken Smooth Time ve Dead Zone/Soft Zone çalışmalı; Z sabit kalmalı.
- Renderer sınırları açıkken nesnelerin görünür boyutu, kapalıyken hedef konumları kapsanmalı.
- Kamera hızla uzaklaşan gruba hemen alan açmalı; grup daralınca Zoom Damping ile yakınlaşmalı.
- Boş, silinmiş ve etkin olmayan grup üyeleri hata oluşturmamalı; boş grup normal takibe ve temel boyuta dönmeli.
- Rotate To Group gruba dönmeli; etkin Look At dönüşte önceliğini korumalı.
- Grup kadrajı ve View Expansion birlikte çalışmalı; açma/kapamada boyut birikmemeli.
- Scene üzerinde grup sınırları/üyeleri görünmeli ve Show Scene Preview ile kapanmalı.
