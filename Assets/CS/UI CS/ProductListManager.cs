using UnityEngine; 
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;
using ARFurniture;

#region 產品 JSON 資料結構定義

// 產品資料物件，用來存放解析後的產品資訊
[System.Serializable]
public class ProductData
{
    public string modelURL;           // 對應 JSON 中的 model_url，3D 模型檔案的位址
    public string productName;        // 對應 JSON 中的 name，產品名稱
    public float price;               // 對應 JSON 中的 price_string，轉換為 float 型態的價格
    public string url;                // 對應 JSON 中的 url，產品連結
    public string otherInfo;          // 對應 JSON 中的 description，產品描述資訊
    public Sprite productImage;       // 產品主要顯示圖片
    public List<Sprite> allSprites = new List<Sprite>();  // 對應 JSON 中的 images，所有圖片的 Sprite 列表
    public string sizeOptions;        // 對應 JSON 中的 size_options，尺寸資訊（例如："46 吋 x 61 吋 x 122 吋"）
    public bool from;                 // 區分來源用的旗標，UI1 預設為 false
}

// 與 JSON 結構對應的產品資料類別
[System.Serializable]
public class JSONProduct
{
    public string category;         // 對應 JSON 中的 category，例如 "chair"
    public string name;             // 對應 JSON 中的 name
    public string price_string;     // 對應 JSON 中的 price_string（價格字串）
    public string url;              // 對應 JSON 中的 url
    public string description;      // 對應 JSON 中的 description，產品描述
    public string model_url;        // 對應 JSON 中的 model_url，3D 模型檔案位址
    public string[] size_options;   // 對應 JSON 中的 size_options 陣列
    public string[] images;         // 對應 JSON 中的 images 陣列，包含各圖片的位址
}

// JSON 輔助解析工具，用於處理 JSON 陣列資料
public static class JsonHelper
{
    // 使用 Unity JsonUtility 將 JSON 陣列字串轉換成物件陣列
    public static T[] FromJson<T>(string json)
    {
        // 將 JSON 字串包裝成一個含有 "array" 欄位的物件，方便解析
        string newJson = "{ \"array\": " + json + "}";
        Wrapper<T> wrapper = JsonUtility.FromJson<Wrapper<T>>(newJson);
        return wrapper.array;
    }

    // 用於包裝 JSON 陣列資料的內部類別
    [System.Serializable]
    private class Wrapper<T>
    {
        public T[] array;
    }
}

#endregion

public class ProductListManager : MonoBehaviour
{
    [Header("產品項目 Prefab")]
    public GameObject productItemPrefab;

    [Header("各分類產品的 Content 物件")]
    public Transform chairContent;
    public Transform deskContent;
    public Transform drawerContent;
    public Transform sofaContent;

    [Header("產品資訊面板")]
    public GameObject panelRoot;

    [Header("其他 UI 按鈕")]
    public Button UIbtn;

    // 儲存所有產品資料的字典，key 為分類（例如 "chair"）
    private Dictionary<string, List<ProductData>> allProducts;

    public ModelLoader1 modelLoader1;  // 用於載入特定 UI3 的模型

    [Header("四個分類按鈕")]
    public Button chairBtn;
    public Button sofaBtn;
    public Button deskBtn;
    public Button drawerBtn;

    [Header("四個對應的面板 (Panels)")]
    public GameObject chairPanel;
    public GameObject sofaPanel;
    public GameObject deskPanel;
    public GameObject drawerPanel;

    [Header("商品加載")]
    public GameObject loadingPanel;

    // 儲存所有按鈕與面板的陣列，方便統一管理
    private Button[] allButtons;
    private GameObject[] allPanels;
    // 紀錄目前選擇的分類索引：0 = Chair, 1 = Sofa, 2 = Desk, 3 = Drawer
    private int currentIndex = 0; 

    void Start()
    {
        if (loadingPanel != null)
        {
            loadingPanel.SetActive(true);
            UIbtn.interactable = false;
        }
        // 初始化按鈕與面板陣列
        allButtons = new Button[] { chairBtn, sofaBtn, deskBtn, drawerBtn };
        allPanels = new GameObject[] { chairPanel, sofaPanel, deskPanel, drawerPanel };

        // AdjustPanelHeights();

        // 為每個按鈕設定點擊事件，傳入對應的分類索引
        chairBtn.onClick.AddListener(() => OnClickCategory(0));
        sofaBtn.onClick.AddListener(() => OnClickCategory(1));
        deskBtn.onClick.AddListener(() => OnClickCategory(2));
        drawerBtn.onClick.AddListener(() => OnClickCategory(3));

        // 初始預設顯示第一個分類 (Chair) 的面板，同時更新按鈕樣式
        ShowPanel(0);
        UpdateButtonStyle(0);
    }
    void AdjustPanelHeights()
    {
        // 假設所有面板都在同一個父容器下
        foreach (GameObject panelObj in allPanels)
        {
            RectTransform rt = panelObj.GetComponent<RectTransform>();
            if (rt != null)
            {
                // 取得父容器的 RectTransform（若父容器是 Canvas 則用 Canvas 的 RectTransform）
                RectTransform parentRect = rt.parent as RectTransform;
                float parentHeight = parentRect != null ? parentRect.rect.height : Screen.height;
                
                // 設定 finalY 為父容器高度的 20%
                float finalY = parentHeight * -0.1f;
                
                // 新的高度從 finalY 到父容器底部，即高度 = 父容器高度 - finalY
                float newHeight = parentHeight - finalY;
                
                // 更新 RectTransform 的 sizeDelta (保留寬度不變)
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, newHeight);
                
                // // 如果面板的錨點設定為 Stretch，則可能需要調整 offsetMin 與 offsetMax，
                // // 例如將上邊界設為 -finalY，底邊界設為 0：
                // rt.offsetMax = new Vector2(rt.offsetMax.x, -finalY);
                // rt.offsetMin = new Vector2(rt.offsetMin.x, 0);
            }
        }
    }

    private void Awake()
    {
        // 初始化產品資料字典，針對各產品分類建立 List
        allProducts = new Dictionary<string, List<ProductData>>();
        allProducts["chair"] = new List<ProductData>();
        allProducts["desk"] = new List<ProductData>();
        allProducts["drawer"] = new List<ProductData>();
        allProducts["sofa"] = new List<ProductData>();

        // 開始非同步下載 JSON 產品資料
        StartCoroutine(DownloadProductJson());
    }

    // 非同步下載產品 JSON 資料
    IEnumerator DownloadProductJson()
    {
        string jsonUrl = "https://raw.githubusercontent.com/TWAnnoyingKid/AR_Furniture_App/main/product.json";
        UnityWebRequest www = UnityWebRequest.Get(jsonUrl);
        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("下載 JSON 資料失敗: " + www.error);
        }
        else
        {
            string jsonText = www.downloadHandler.text;
            // 解析 JSON 並填入產品資料字典
            ParseJsonProducts(jsonText);

            // 等待 1 秒，確保圖片下載程序有足夠時間執行
            yield return new WaitForSeconds(1f);

            // 建立各分類的產品 UI 項目
            CreateProductItems("chair", chairContent);
            CreateProductItems("desk", deskContent);
            CreateProductItems("drawer", drawerContent);
            CreateProductItems("sofa", sofaContent);
            if (loadingPanel != null)
            {
                loadingPanel.SetActive(false);
                UIbtn.interactable = true;
            }
        }
    }

    // 解析 JSON 產品資料，並將資料存入 allProducts 字典中
    void ParseJsonProducts(string jsonText)
    {
        JSONProduct[] products = JsonHelper.FromJson<JSONProduct>(jsonText);
        foreach (var jp in products)
        {
            ProductData pd = new ProductData();
            pd.productName = jp.name;
            float.TryParse(jp.price_string, out pd.price);
            pd.url = jp.url;
            pd.otherInfo = jp.description;
            pd.modelURL = jp.model_url;
            pd.sizeOptions = (jp.size_options != null && jp.size_options.Length > 0) ? jp.size_options[0] : "";
            pd.from = false;

            // 非同步下載產品對應的所有圖片
            StartCoroutine(DownloadImagesForProduct(jp.images, pd));

            // 根據 JSON 中的 category 屬性，加入相對應的分類列表（轉換為小寫）
            string category = jp.category.ToLower();
            if (!allProducts.ContainsKey(category))
            {
                allProducts[category] = new List<ProductData>();
            }
            allProducts[category].Add(pd);
        }
    }

    // 非同步下載產品圖片，並將下載到的圖片轉換為 Sprite
    IEnumerator DownloadImagesForProduct(string[] imageUrls, ProductData pd)
    {
        foreach (string imageUrl in imageUrls)
        {
            UnityWebRequest request = UnityWebRequestTexture.GetTexture(imageUrl);
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                Texture2D texture = DownloadHandlerTexture.GetContent(request);
                Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                // 若尚未指定主要圖片，則將第一張下載的圖片設為主要圖片
                if (pd.productImage == null)
                {
                    pd.productImage = sprite;
                }
                pd.allSprites.Add(sprite);
            }
            else
            {
                Debug.LogWarning("下載圖片失敗: " + imageUrl);
            }
        }
    }

    // 根據指定分類，產生產品項目 UI，並加入至對應的 Content 下
    private void CreateProductItems(string category, Transform parent)
    {
        if (!allProducts.ContainsKey(category)) return;
        List<ProductData> list = allProducts[category];

        foreach (var pd in list)
        {
            GameObject itemObj = Instantiate(productItemPrefab, parent);
            var productImageTransform   = itemObj.transform.Find("ProductImage")?.GetComponent<Image>();
            RectTransform btnRect = itemObj.GetComponent<RectTransform>();
            btnRect.sizeDelta = new Vector2(400f, 400f);

            var nameTxt = itemObj.transform.Find("NameText")?.GetComponentInChildren<TextMeshProUGUI>();
            var priceTxt = itemObj.transform.Find("PriceText")?.GetComponentInChildren<TextMeshProUGUI>();
            var arBtn = itemObj.transform.Find("ViewARButton")?.GetComponent<Button>();
            var infoBtn = itemObj.transform.Find("InfoButton")?.GetComponent<Button>();
            
            if (productImageTransform  != null){
                Transform imgTransform = productImageTransform.transform.Find("Image");
                if (imgTransform != null){
                    Image img = imgTransform.GetComponent<Image>();
                    if (img != null && pd.productImage != null){
                        img.sprite = pd.productImage;

                        float fixedSize = 400f; // 固定尺寸依據
                        float spriteWidth = pd.productImage.rect.width;
                        float spriteHeight = pd.productImage.rect.height;
                        float calculatedWidth = fixedSize * (spriteWidth / spriteHeight);
                        float calculatedHeight = fixedSize * (spriteHeight / spriteWidth);

                        RectTransform imgRect = img.GetComponent<RectTransform>();
                        if (calculatedWidth > fixedSize)
                        {
                            // 寬度超過 400，則固定寬度 400，高度依比例調整
                            imgRect.sizeDelta = new Vector2(fixedSize, calculatedHeight);
                        }
                        else
                        {
                            // 否則固定高度 400，寬度依比例調整
                            imgRect.sizeDelta = new Vector2(calculatedWidth, fixedSize);
                        }
                        // 置中
                        imgRect.anchoredPosition = Vector2.zero;
                    }
                }
                
                
            }
            if (nameTxt != null)
                nameTxt.text = pd.productName;
            if (priceTxt != null)
                priceTxt.text = "$" + pd.price;

            
            if (infoBtn != null)// 設定產品資訊按鈕，點擊後顯示詳細資訊
            {
                ProductData capturedPd = pd;
                infoBtn.onClick.AddListener(() =>
                {
                    UIbtn.interactable = false;
                    FurnitureListToggle.Instance.ForceSlideOut();
                    InfoPanelController.Instance.ShowProductInfo(capturedPd);
                });
            }

            
            if (arBtn != null)// 設定 AR 按鈕，點擊後載入對應的 3D 模型
            {
                ProductData capturedPd = pd;
                arBtn.onClick.AddListener(() =>
                {
                    FurnitureListToggle.Instance.ForceSlideOut();
                    
                    modelLoader1.SetModelToLoad(capturedPd.modelURL, capturedPd);// 透過 ModelLoader1 載入 3D 模型
                });
            }
        }
    }
    private void OnClickCategory(int index)
    {
        currentIndex = index;
        ShowPanel(index);
        UpdateButtonStyle(index);
    }

    // 顯示指定索引的面板，並隱藏其他所有面板
    private void ShowPanel(int index)
    {
        for (int i = 0; i < allPanels.Length; i++)
        {
            allPanels[i].SetActive(false);  // 隱藏所有面板
        }
        allPanels[index].SetActive(true);   // 只顯示選取的面板
    }
    private void UpdateButtonStyle(int selectedIndex)
    {
        for (int i = 0; i < allButtons.Length; i++)
        {
            var rt = allButtons[i].GetComponent<RectTransform>();
            var img = allButtons[i].GetComponent<Image>();
            var txt = allButtons[i].GetComponentInChildren<TextMeshProUGUI>();

            if (i == selectedIndex)
            {
                // 選取的按鈕：尺寸較大、背景顏色較深、文字顏色為白色
                rt.sizeDelta = new Vector2(400, 180);
                img.color = new Color32(46, 0, 149, 255);
                txt.color = Color.white;
            }
            else
            {
                // 未選取的按鈕：尺寸較小、背景為白色、文字顏色為黑色
                rt.sizeDelta = new Vector2(350, 150);
                img.color = Color.white;
                txt.color = Color.black;
            }
        }
    }
}