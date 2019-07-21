using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using System.Windows.Input;
using Clio.Utilities.Collections;
using ff14bot.Enums;
using ff14bot.Managers;
using Magitek.Commands;
using Magitek.Extensions;
using Magitek.Models.Astrologian;
using Magitek.Utilities;
using Newtonsoft.Json;
using PropertyChanged;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace Magitek.ViewModels
{
    [AddINotifyPropertyChangedInterface]
    public partial class AstrologianCardRules : INotifyPropertyChanged
    {
        private static AstrologianCardRules _instance;
        public static AstrologianCardRules Instance => _instance ?? (_instance = new AstrologianCardRules());

        private AstrologianCardRules()
        {
            if (AstrologianSettings.Instance.CardRules == null)
                AstrologianSettings.Instance.CardRules = AstrologianDefaultCardRules.DefaultCardRules;

            CardRules = new ObservableCollection<CardRule>(AstrologianSettings.Instance.CardRules);
            CollectionViewSource = System.Windows.Data.CollectionViewSource.GetDefaultView(CardRules);
            CollectionViewSource.SortDescriptions.Add(new SortDescription("CardPriority", ListSortDirection.Ascending));
            ResetCollectionViewSource();
            ReloadUiElements();
        }

        public ICollectionView CollectionViewSource { get; set; }
        public ObservableCollection<CardRule> CardRules { get; set; }
        public CardRule SelectedCardRules { get; set; }
        public int SelectedCardRuleIndex { get; set; } = 0;

        #region Filter Properties
        private string _logicType;

        public string LogicType
        {
            get => _logicType;
            set
            {
                _logicType = value;
                ResetCollectionViewSource();
                OnPropertyChanged();
            }
        }

        private string _cardType;

        public string CardType
        {
            get => _cardType;
            set
            {
                _cardType = value;
                ResetCollectionViewSource();
                OnPropertyChanged();
            }
        }
        #endregion

        #region Collection View Source Reset
        public void ResetCollectionViewSource()
        {
            CollectionViewSource.Filter = r =>
            {
                var cardRule = (CardRule) r;

                if (cardRule == null)
                    return false;

                switch (LogicType)
                {
                    case "Solo":
                        if (cardRule.LogicType != CardLogicType.Solo)
                            return false;
                        break;
                    case "Group":
                        if (cardRule.LogicType != CardLogicType.Party)
                            return false;
                        break;
                    case "Raid":
                        if (cardRule.LogicType != CardLogicType.LargeParty)
                            return false;
                        break;
                    case "Pvp":
                        if (cardRule.LogicType != CardLogicType.Pvp)
                            return false;
                        break;

                    default:
                        break;
                }

                switch (CardType)
                {
                    case "MeleeDPS":
                        if (cardRule.CardType != AstrologianCardType.MeleeDPS)
                            return false;
                        break;
                    case "UpgradedMeleeDPS":
                        if (cardRule.CardType != AstrologianCardType.UpgradedMeleeDPS)
                            return false;
                        break;

                    case "RangedDPS":
                        if (cardRule.CardType != AstrologianCardType.RangedDPS)
                            return false;
                        break;

                    case "UpgradedRangedDPS":
                        if (cardRule.CardType != AstrologianCardType.UpgradedRangedDPS)
                            return false;
                        break;

                    default:
                        break;
                }

                return true;
            };            

            CollectionViewSource.Refresh();
        }
        #endregion

        #region Save / Load

        public ICommand ApplyCardRules => new DelegateCommand(ResetAstrologianCardRules);

        public ICommand SaveCardRules => new DelegateCommand(() =>
        {
            var saveFile = new SaveFileDialog
            {
                Filter = "json files (*.json)|*.json",
                Title = "Save Card Rules File",
                OverwritePrompt = true
            };

            if (saveFile.ShowDialog() != true)
                return;

            var data = JsonConvert.SerializeObject(CardRules, Formatting.Indented);
            File.WriteAllText(saveFile.FileName, data);
            Logger.Write($@"Card Rules Exported Under {saveFile.FileName} ");
        });

        public ICommand LoadCardRules => new DelegateCommand(() =>
        {
            var loadFile = new OpenFileDialog()
            {
                Filter = "json files (*.json)|*.json",
                Title = "Open Card Rules File"
            };

            if (loadFile.ShowDialog() == true)
            {
                AstrologianSettings.Instance.CardRules = JsonConvert.DeserializeObject<ObservableCollection<CardRule>>(File.ReadAllText(loadFile.FileName));
                CardRules = new ObservableCollection<CardRule>(AstrologianSettings.Instance.CardRules);
                CollectionViewSource = System.Windows.Data.CollectionViewSource.GetDefaultView(CardRules);
                CollectionViewSource.SortDescriptions.Add(new SortDescription("CardPriority", ListSortDirection.Ascending));
                ResetCollectionViewSource();
            }

            if (string.IsNullOrEmpty(loadFile.FileName))
                return;

            Logger.Write($@"Card Rules Loaded");
            SelectedCardRuleIndex = 0;
        });

        public ICommand LoadDefaultCardRules => new DelegateCommand(() =>
        {
            Logger.WriteInfo(@"Resetting Card Rules to Defaults. Writing " +
                             AstrologianDefaultCardRules.DefaultCardRules.Count + " Rules to Settings.");

            CardRules = new ObservableCollection<CardRule>(AstrologianDefaultCardRules.DefaultCardRules);
            CollectionViewSource = System.Windows.Data.CollectionViewSource.GetDefaultView(CardRules);
            CollectionViewSource.SortDescriptions.Add(new SortDescription("CardPriority", ListSortDirection.Ascending));
            ResetCollectionViewSource();
            SelectedCardRuleIndex = 0;
        });

        public void ResetAstrologianCardRules()
        {
            if (CardRules == null)
                return;

            AstrologianSettings.Instance.CardRules = new ObservableCollection<CardRule>(CardRules);
            ResetCollectionViewSource();

            if (BaseSettings.Instance.CurrentRoutine != "Astrologian")
                return;

            Logger.WriteInfo("New Card Rules Applied");
        }

        #endregion

        #region Add New Card Command
        public ICommand AddNewCard => new DelegateCommand(() =>
        {
            var newPriority = 1;

            if (CardRules.Any())
            {
                newPriority = CardRules.Max(r => r.CardPriority) + 1;
            }
            
            var newCardLogicType = CardLogicType.Party;
            var newCardType = AstrologianCardType.None;

            switch (LogicType)
            {
                case ("Group"):
                    newCardLogicType = CardLogicType.Party;
                    break;
                case ("Raid"):
                    newCardLogicType = CardLogicType.LargeParty;
                    break;
                case ("Solo"):
                    newCardLogicType = CardLogicType.Solo;
                    break;
                case ("Pvp"):
                    newCardLogicType = CardLogicType.Pvp;
                    break;
                default:
                    newCardLogicType = CardLogicType.LargeParty;
                    break;
            }

            switch (CardType)
            {
                case ("RangeDPS"):
                    newCardType = AstrologianCardType.RangedDPS;
                    break;
                case ("UpgradedRangeDPS"):
                    newCardType = AstrologianCardType.UpgradedRangedDPS;
                    break;
                case ("MeleeDPS"):
                    newCardType = AstrologianCardType.MeleeDPS;
                    break;
                case ("UpgradedMeleeDPS"):
                    newCardType = AstrologianCardType.UpgradedMeleeDPS;
                    break;
            }

            var newCard = new CardRule
            {
                CardPriority = newPriority,
                CardType = newCardType,
                LogicType = newCardLogicType
            };

            Logger.WriteInfo($"Adding new card at {newPriority}");
            CardRules.Add(newCard);
            //CardRules.Insert(newPriority, newCard);
            ResetCollectionViewSource();
            var newSelectedCardIndex = CollectionViewSource.Cast<CardRule>().Count() - 1;
            SelectedCardRuleIndex = newSelectedCardIndex;
        });
        #endregion

        #region Delete Selected Card Command
        public ICommand RemSelCard => new DelegateCommand(() =>
        {
            if (SelectedCardRules == null)
                return;

            var oldSelectedCardRuleIndex = SelectedCardRuleIndex;
            CardRules.Remove(SelectedCardRules);
            ResetCollectionViewSource();
            SelectedCardRuleIndex = oldSelectedCardRuleIndex;
        });
        #endregion

        // Toggle Buttons

        #region IsJob
        public bool IsJobAstrologian { get; set; }
        public bool IsJobWhiteMage { get; set; }
        public bool IsJobScholar { get; set; }
        public bool IsJobPaladin { get; set; }
        public bool IsJobWarrior { get; set; }
        public bool IsJobDarkKnight { get; set; }
        public bool IsJobBard { get; set; }
        public bool IsJobMachinist { get; set; }
        public bool IsJobBlackMage { get; set; }
        public bool IsJobRedMage { get; set; }
        public bool IsJobSummoner { get; set; }
        public bool IsJobDragoon { get; set; }
        public bool IsJobMonk { get; set; }
        public bool IsJobNinja { get; set; }
        public bool IsJobSamurai { get; set; }
        public bool IsJobDancer { get; set; }
        public bool IsJobGunbreaker { get; set; }

        public void ResetIsJobSettingsUi()
        {
            try
            {
                if (SelectedCardRules == null)
                    return;

                if (SelectedCardRules.TargetConditions == null)
                {
                    SelectedCardRules.TargetConditions = new TargetConditions();
                }

                if (SelectedCardRules.TargetConditions.IsJob == null)
                {
                    SelectedCardRules.TargetConditions.IsJob =
                        new AsyncObservableCollection<ClassJobType>();
                }

                SelectedCardRules.TargetConditions.IsJob.Clear();

                if (IsJobAstrologian)
                {
                    SelectedCardRules.TargetConditions.IsJob.Add(ClassJobType.Astrologian);
                }

                if (IsJobWhiteMage)
                {
                    SelectedCardRules.TargetConditions.IsJob.Add(ClassJobType.WhiteMage);
                    SelectedCardRules.TargetConditions.IsJob.Add(ClassJobType.Conjurer);
                }

                if (IsJobScholar)
                {
                    SelectedCardRules.TargetConditions.IsJob.Add(ClassJobType.Scholar);
                }

                if (IsJobPaladin)
                {
                    SelectedCardRules.TargetConditions.IsJob.Add(ClassJobType.Paladin);
                    SelectedCardRules.TargetConditions.IsJob.Add(ClassJobType.Gladiator);
                }

                if (IsJobWarrior)
                {
                    SelectedCardRules.TargetConditions.IsJob.Add(ClassJobType.Warrior);
                    SelectedCardRules.TargetConditions.IsJob.Add(ClassJobType.Marauder);
                }

                if (IsJobDarkKnight)
                {
                    SelectedCardRules.TargetConditions.IsJob.Add(ClassJobType.DarkKnight);
                }

                if (IsJobBard)
                {
                    SelectedCardRules.TargetConditions.IsJob.Add(ClassJobType.Bard);
                    SelectedCardRules.TargetConditions.IsJob.Add(ClassJobType.Archer);
                }

                if (IsJobMachinist)
                {
                    SelectedCardRules.TargetConditions.IsJob.Add(ClassJobType.Machinist);
                }

                if (IsJobBlackMage)
                {
                    SelectedCardRules.TargetConditions.IsJob.Add(ClassJobType.BlackMage);
                    SelectedCardRules.TargetConditions.IsJob.Add(ClassJobType.Thaumaturge);
                }

                if (IsJobRedMage)
                {
                    SelectedCardRules.TargetConditions.IsJob.Add(ClassJobType.RedMage);
                }

                if (IsJobSummoner)
                {
                    SelectedCardRules.TargetConditions.IsJob.Add(ClassJobType.Summoner);
                    SelectedCardRules.TargetConditions.IsJob.Add(ClassJobType.Arcanist);
                }

                if (IsJobDragoon)
                {
                    SelectedCardRules.TargetConditions.IsJob.Add(ClassJobType.Dragoon);
                    SelectedCardRules.TargetConditions.IsJob.Add(ClassJobType.Lancer);
                }

                if (IsJobMonk)
                {
                    SelectedCardRules.TargetConditions.IsJob.Add(ClassJobType.Monk);
                    SelectedCardRules.TargetConditions.IsJob.Add(ClassJobType.Pugilist);
                }

                if (IsJobNinja)
                {
                    SelectedCardRules.TargetConditions.IsJob.Add(ClassJobType.Ninja);
                    SelectedCardRules.TargetConditions.IsJob.Add(ClassJobType.Rogue);
                }

                if (IsJobSamurai)
                {
                    SelectedCardRules.TargetConditions.IsJob.Add(ClassJobType.Samurai);
                }

                if (IsJobDancer)
                {
                    SelectedCardRules.TargetConditions.IsJob.Add(ClassJobType.Dancer);
                }

                if (IsJobGunbreaker)
                {
                    SelectedCardRules.TargetConditions.IsJob.Add(ClassJobType.Gunbreaker);
                }
            }
            catch
            {
                if (BaseSettings.Instance.GeneralSettings.DebugCastingCallerMemberName)
                {
                    Logger.WriteInfo(@"Something weird just happened with Card Rules. This is a known issue and is being worked on.");
                }
            }
        }

        private void LoadIsJobSettingsUi()
        {
            if (SelectedCardRules?.TargetConditions == null)
            {
                IsJobAstrologian = false;
                IsJobWhiteMage = false;
                IsJobScholar = false;
                IsJobPaladin = false;
                IsJobWarrior = false;
                IsJobDarkKnight = false;
                IsJobBard = false;
                IsJobMachinist = false;
                IsJobBlackMage = false;
                IsJobRedMage = false;
                IsJobSummoner = false;
                IsJobDragoon = false;
                IsJobMonk = false;
                IsJobNinja = false;
                IsJobSamurai = false;
                IsJobGunbreaker = false;
                IsJobDancer = false;
                return;
            }

            var tempList = new List<ClassJobType>(SelectedCardRules.TargetConditions.IsJob);

            IsJobAstrologian = tempList.Contains(ClassJobType.Astrologian);
            IsJobWhiteMage = tempList.Contains(ClassJobType.WhiteMage);
            IsJobScholar = tempList.Contains(ClassJobType.Scholar);
            IsJobPaladin = tempList.Contains(ClassJobType.Paladin);
            IsJobWarrior = tempList.Contains(ClassJobType.Warrior);
            IsJobDarkKnight = tempList.Contains(ClassJobType.DarkKnight);
            IsJobBard = tempList.Contains(ClassJobType.Bard);
            IsJobMachinist = tempList.Contains(ClassJobType.Machinist);
            IsJobBlackMage = tempList.Contains(ClassJobType.BlackMage);
            IsJobRedMage = tempList.Contains(ClassJobType.RedMage);
            IsJobSummoner = tempList.Contains(ClassJobType.Summoner);
            IsJobDragoon = tempList.Contains(ClassJobType.Dragoon);
            IsJobMonk = tempList.Contains(ClassJobType.Monk);
            IsJobNinja = tempList.Contains(ClassJobType.Ninja);
            IsJobSamurai = tempList.Contains(ClassJobType.Samurai);
            IsJobDancer = tempList.Contains(ClassJobType.Dancer);
            IsJobGunbreaker = tempList.Contains(ClassJobType.Gunbreaker);
        }
        #endregion

        #region TargetHasTarget
        public bool TargetHasTargetYes { get; set; }
        public bool TargetHasTargetNo { get; set; }
        public bool TargetHasTargetDoesntMatter { get; set; }

        public void ResetTargetHasTargetSettingsUi()
        {
            if (SelectedCardRules == null)
                return;

            if (SelectedCardRules.TargetConditions == null)
            {
                SelectedCardRules.TargetConditions = new TargetConditions();
            }

            if (TargetHasTargetYes)
            {
                SelectedCardRules.TargetConditions.HasTarget = true;
            }

            if (TargetHasTargetNo)
            {
                SelectedCardRules.TargetConditions.HasTarget = false;
            }

            if (TargetHasTargetDoesntMatter)
            {
                SelectedCardRules.TargetConditions.HasTarget = null;
            }
        }

        private void LoadTargetHasTargetSettingsUi()
        {
            if (SelectedCardRules?.TargetConditions == null)
                return;

            TargetHasTargetYes = false;
            TargetHasTargetNo = false;
            TargetHasTargetDoesntMatter = false;

            switch (SelectedCardRules.TargetConditions.HasTarget)
            {
                case null:
                    TargetHasTargetDoesntMatter = true;
                    return;
                case true:
                    TargetHasTargetYes = true;
                    return;
                case false:
                    TargetHasTargetNo = true;
                    return;
                default:
                    break;
            }
        }
        #endregion  

        public void ReloadUiElements()
        {
            LoadIsJobSettingsUi();
            LoadTargetHasTargetSettingsUi();
        }

        #region Property Changed
        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion
    }
}
