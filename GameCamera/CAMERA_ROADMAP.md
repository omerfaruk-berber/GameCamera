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
- Hedef atanmamışsa ya da silinmişse kamerayı bulunduğu yerde tutmak.
- Başka kodların `SetTarget` ve `SnapToTarget` ile kamerayı yönlendirebilmesi.

Bu gövde standart XY düzlemindeki, dönüşü `(0, 0, 0)` olan 2B kamera içindir.
Takip hedefi kameradan bağımsız olmalı; kamera hedefin alt nesnesi yapılmamalı.

## 2. Aşama — Oyun türüne göre takip (sonra)

İhtiyaç seçildikçe ayrı dosyalar ekle:

- `PlatformerCamera2D.cs`: yatay ağırlıklı takip ve hareket yönüne doğru bakış.
- `TopDownCamera2D.cs`: üstten görünüm için kadraj davranışı.
- `CameraDeadZone2D.cs`: hedef küçük hareketler yaparken kamerayı sabit tutan alan.

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

// Takibi bırak; kamera son konumunda kalır.
cameraController.SetTarget(null);
```

Burada `cameraController`, Inspector üzerinden bağlanan bir `MainCamera`
referansıdır. `Target` özelliği mevcut hedefi dışarıdan okumak için kullanılabilir.

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

Bileşen etkinleştiğinde Camera üzerindeki `Size` değerini başlangıç boyutu
olarak alır. Etkinken boyutu bu bileşen yönetir; başlangıç boyutunu değiştirmek
için bileşeni kapat, Camera `Size` değerini ayarla ve yeniden aç.
Bileşen kapatıldığında eklediği genişleme ve kadraj kayması geri alınır.
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
Çizimler standart 2B oyun düzleminde (`Z = 0`) gösterilir ve Camera ayarlarını
veya nesnelerin konumlarını değiştirmez. Önizleme, Camera'nın görüntü oranını kullanır.

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
