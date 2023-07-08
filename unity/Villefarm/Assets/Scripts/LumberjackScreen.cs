using System;
using System.Collections;
using Frictionless;
using Lumberjack.Accounts;
using Solana.Unity.SDK;
using Solana.Unity.Wallet;
using Solana.Unity.Wallet.Bip39;
using SolPlay.Scripts.Services;
using SolPlay.Scripts.Ui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LumberjackScreen : MonoBehaviour
{
    public Button LoginButton;
    public Button LoginWalletAdapterButton;
    
    public Button PlantPeasantButton;
    public Button PlantBreadmakerButton;
    public Button PlantBeerbrewerButton;
    public Button PlantBlacksmithButton;
    public Button PlantSolanaDeveButton;

    public Sprite Peasant;
    public Sprite Breadmaker;
    public Sprite Beerbrewer;
    public Sprite Blacksmith;
    public Sprite SolanaDeveloper;

    public Image CurrentPlotImage;
    
    public Button HarvestButton;
    
    public Button RevokeSessionButton;
    public Button NftsButton;
    public Button InitGameDataButton;

    public TextMeshProUGUI EnergyAmountText;
    public TextMeshProUGUI WoodAmountText;
    public TextMeshProUGUI NextEnergyInText;

    public GameObject LoggedInRoot;
    public GameObject NotInitializedRoot;
    public GameObject InitializedRoot;
    public GameObject NotLoggedInRoot;

    public PlayerData CurrentPlayerData;
    public Plot CurrentPlotData;

    void Start()
    {
        LoggedInRoot.SetActive(false);
        NotLoggedInRoot.SetActive(true);
        
        LoginButton.onClick.AddListener(OnLoginClicked);
        LoginWalletAdapterButton.onClick.AddListener(OnLoginWalletAdapterButtonClicked);
        
        PlantPeasantButton.onClick.AddListener(OnPlantPeasantButtonClicked);
        PlantBreadmakerButton.onClick.AddListener(OnPlantBreadmakerButtonClicked);
        PlantBeerbrewerButton.onClick.AddListener(OnPlantBeerbrewerButtonClicked);
        PlantBlacksmithButton.onClick.AddListener(OnPlantBlacksmithButtonClicked);
        PlantSolanaDeveButton.onClick.AddListener(OnPlantSolanaDevButtonClicked);
        
        HarvestButton.onClick.AddListener(OnHarvestClicked);
        
        RevokeSessionButton.onClick.AddListener(OnRevokeSessionButtonClicked);
        NftsButton.onClick.AddListener(OnNftsButtonnClicked);
        InitGameDataButton.onClick.AddListener(OnIitGameDataButtonClicked);
        LumberjackService.OnPlayerDataChanged += OnPlayerDataChanged;
        LumberjackService.OnPlotDataChanged += OnPlotDataChanged;

        StartCoroutine(UpdateNextEnergy());
        
        LumberjackService.OnInitialDataLoaded += UpdateContent;

        Web3.OnLogin += OnLogin;
    }


    private void OnLogin(Account obj)
    {
        UpdateContent();
    }
    
    private async void OnIitGameDataButtonClicked()
    {
        var res = await LumberjackService.Instance.InitGameDataAccount();
        Debug.Log(res.RawRpcResponse);
    }

    private void OnNftsButtonnClicked()
    {
        ServiceFactory.Resolve<UiService>().OpenPopup(UiService.ScreenType.NftListPopup, new NftListPopupUiData(false, Web3.Wallet));
    }

    private void UpdateContent()
    {
        var isInitialized = LumberjackService.Instance.IsInitialized();
        LoggedInRoot.SetActive(Web3.Account != null);
        NotInitializedRoot.SetActive(!isInitialized);
        InitializedRoot.SetActive(isInitialized);

        NotLoggedInRoot.SetActive(Web3.Account == null);
        
        if (CurrentPlayerData != null)
        {
            EnergyAmountText.text = CurrentPlayerData.Energy.ToString();
            WoodAmountText.text = CurrentPlayerData.Gold.ToString();
        }
        
        if (CurrentPlotData != null)
        {
            var plantedAtTime = CurrentPlotData.PlantedAt;
            var timePassed = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - plantedAtTime;
            
            var plantReadyIn = LumberjackService.TIME_TO_GROW - timePassed;

            if (plantReadyIn > 0)
            {
                NextEnergyInText.text = plantReadyIn.ToString();
            }
            else
            {
                NextEnergyInText.text = CurrentPlotData.HumanType == "" ? "Plant smth!" : "Human ready!";
            }

            HarvestButton.interactable = plantReadyIn <= 0 && CurrentPlotData.HumanType != "";
             
            switch (CurrentPlotData.HumanType)
            {
                case "peasant":
                    CurrentPlotImage.sprite = Peasant;
                    break;
                case "breadmaker":
                    CurrentPlotImage.sprite = Breadmaker;
                    break;
                case "beerbrewer":
                    CurrentPlotImage.sprite = Beerbrewer;
                    break;
                case "blacksmith":
                    CurrentPlotImage.sprite = Blacksmith;
                    break;
                case "solanadev":
                    CurrentPlotImage.sprite = SolanaDeveloper;
                    break;
                default:
                    CurrentPlotImage.sprite = null;
                    break;
            }

        }
    }

    private async void OnRevokeSessionButtonClicked()
    {
        var res =  await LumberjackService.Instance.RevokeSession();
        Debug.Log("Revoked Session: " + res.Account);
    }

    private async void OnLoginWalletAdapterButtonClicked()
    {
        await Web3.Instance.LoginWalletAdapter();
    }

    private IEnumerator UpdateNextEnergy()
    {
        while (true)
        {
            yield return new WaitForSeconds(1);
            UpdateContent();
        }
    }

    private void OnPlayerDataChanged(PlayerData playerData)
    {
        CurrentPlayerData = playerData;
        UpdateContent();
    }

    private void OnPlotDataChanged(Plot newPlotData)
    {
        CurrentPlotData = newPlotData;
        UpdateContent();
    }

    private async void OnPlantPeasantButtonClicked()
    {
        SendPlantTransaction("peasant");
    }
    private async void OnPlantBreadmakerButtonClicked()
    {
        SendPlantTransaction("breadmaker");
    }
    private async void OnPlantBeerbrewerButtonClicked()
    {
        SendPlantTransaction("beerbrewer");
    }
    private async void OnPlantBlacksmithButtonClicked()
    {
        SendPlantTransaction("blacksmith");
    }
    private async void OnPlantSolanaDevButtonClicked()
    {
        SendPlantTransaction("solanadev");
    }
    
    private async void OnHarvestClicked()
    {
        var res =  await LumberjackService.Instance.Harvest(true);
        Debug.Log(res.RawRpcResponse);
    }
    
    private async void SendPlantTransaction(string humanType)
    {
        var res =  await LumberjackService.Instance.Plant(true, humanType);
        Debug.Log(res.RawRpcResponse);
    }

    private async void OnLoginClicked()
    {
        var newMnemonic = new Mnemonic(WordList.English, WordCount.Twelve);

        // Dont use this one for production.
        var account = await Web3.Instance.LoginInGameWallet("1234") ??
                      await Web3.Instance.CreateAccount(newMnemonic.ToString(), "1234");

    }
}
