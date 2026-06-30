using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LobbyMenu : MonoBehaviour
{
    [SerializeField] Button createLobbyBtn;
    [SerializeField] Button listLobbiesBtn;
    [SerializeField] Button joinLobbyBtn;
    [SerializeField] Button quickJoinBtn;
    [SerializeField] TMP_InputField codeInputField;

    private string lobbyCode;

    [SerializeField] TestLobby tstLobby;

    void Awake()
    {
        createLobbyBtn.onClick.AddListener(() =>
        {
            tstLobby.CreateLobby();
        });

        listLobbiesBtn.onClick.AddListener(() =>
        {
            tstLobby.ListLobbies();
        });

        joinLobbyBtn.onClick.AddListener(() =>
        {
            lobbyCode = codeInputField.text.ToString();
            tstLobby.JoinLobbyByCode(lobbyCode);
        });

        quickJoinBtn.onClick.AddListener(() =>
        {
            tstLobby.QuickJoinLobby();
        });
    }
}
