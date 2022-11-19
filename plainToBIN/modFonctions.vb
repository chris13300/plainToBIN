Imports VB = Microsoft.VisualBasic

Module modFonctions
    Public processus As System.Diagnostics.Process
    Public entree As System.IO.StreamWriter
    Public sortie As System.IO.StreamReader

    Public Sub chargerMoteur(chemin As String)
        Dim chaine As String

        processus = New System.Diagnostics.Process()

        processus.StartInfo.RedirectStandardOutput = True
        processus.StartInfo.UseShellExecute = False
        processus.StartInfo.RedirectStandardInput = True
        processus.StartInfo.CreateNoWindow = True
        processus.StartInfo.WorkingDirectory = My.Application.Info.DirectoryPath
        processus.StartInfo.FileName = chemin
        processus.Start()
        processus.PriorityClass = 64 '64 (idle), 16384 (below normal), 32 (normal), 32768 (above normal), 128 (high), 256 (realtime)

        entree = processus.StandardInput
        sortie = processus.StandardOutput

        entree.WriteLine("uci")
        chaine = ""
        While InStr(chaine, "uciok") = 0
            chaine = sortie.ReadLine
            Threading.Thread.Sleep(1)
        End While

        entree.WriteLine("setoption name threads value 1")

        entree.WriteLine("isready")
        chaine = ""
        While InStr(chaine, "readyok") = 0
            chaine = sortie.ReadLine
            Threading.Thread.Sleep(1)
        End While
    End Sub

    Public Sub dechargerMoteur()
        entree.Close()
        sortie.Close()
        processus.Close()

        entree = Nothing
        sortie = Nothing
        processus = Nothing
    End Sub

    Public Function defragBIN(cheminBIN As String, profMin As Integer) As String
        Dim tabBIN(0) As Byte, i As Long, tabNEW() As Byte, offset As Long
        Dim tabTampon(23) As Byte, compteur As Integer, nbSuppression As Integer
        Dim message As String, pos As Long
        Dim lectureBIN As IO.FileStream, posLecture As Long, tailleBIN As Long, tailleTampon As Long, reservation As Boolean

        message = ""

        If My.Computer.FileSystem.FileExists(cheminBIN & ".bak") Then
            My.Computer.FileSystem.DeleteFile(cheminBIN & ".bak")
        End If

        posLecture = 0
        tailleBIN = FileLen(cheminBIN)
        tailleTampon = tailleBIN
        i = 50
        lectureBIN = New IO.FileStream(cheminBIN, IO.FileMode.Open, IO.FileAccess.Read, IO.FileShare.ReadWrite)

        compteur = 0
        nbSuppression = 0

        While posLecture < tailleBIN
            If posLecture + tailleTampon <= tailleBIN Then
                reservation = False
                Do
                    Try
                        ReDim tabBIN(tailleTampon - 1)
                        reservation = True
                    Catch ex As Exception
                        i = i - 1
                        tailleTampon = 24 * i * 1000000
                    End Try
                Loop Until reservation
                lectureBIN.Read(tabBIN, 0, tabBIN.Length)
            Else
                tailleTampon = tailleBIN - posLecture
                ReDim tabBIN(tailleTampon - 1)
                lectureBIN.Read(tabBIN, 0, tabBIN.Length)
            End If

            ReDim tabNEW(UBound(tabBIN))
            pos = 0
            offset = 0
            Do
                Array.Copy(tabBIN, pos, tabTampon, 0, 24)
                If profMin <= tabTampon(8) Then
                    Array.Copy(tabTampon, 0, tabNEW, offset * 24, 24)
                    offset = offset + 1
                Else
                    nbSuppression = nbSuppression + 1
                End If

                compteur = compteur + 1
                pos = pos + 24
            Loop While pos < tabBIN.Length

            tabBIN = Nothing

            ReDim Preserve tabNEW(offset * 24 - 1)
            My.Computer.FileSystem.WriteAllBytes(cheminBIN & ".bak", tabNEW, True)

            tabNEW = Nothing

            posLecture = lectureBIN.Position
        End While

        lectureBIN.Close()

        message = "info string " & nomFichier(cheminBIN) & " -> Total moves: " & compteur & ". Empty moves: " & nbSuppression & ". Fragmentation: " & Format(nbSuppression / compteur, "0.00%") & vbCrLf
        message = message & "info string Saved " & Format(compteur - nbSuppression, "0 moves") & " to " & nomFichier(cheminBIN) & " file"

        If compteur > nbSuppression Then
            My.Computer.FileSystem.DeleteFile(cheminBIN)
            My.Computer.FileSystem.RenameFile(cheminBIN & ".bak", nomFichier(cheminBIN))
        End If

        Return message
    End Function

    Public Function droite(texte As String, longueur As Integer) As String
        If longueur > 0 Then
            Return VB.Right(texte, longueur)
        Else
            Return ""
        End If
    End Function

    Public Sub entreeBIN(ByRef tabBIN() As Byte, positionEPD As String, coupUCI As String, scoreCP As Integer, facteur As Integer, prof As Integer, performance As Integer, entreeDefrag As System.IO.StreamWriter, sortieDefrag As System.IO.StreamReader)
        '    inversed key (8 octets) | Depth |          | score       | inv. move |       | Performance |
        'hex  0  1  2  3  4  5  6  7 |  8    |  9  A  B |  C  D  E  F |  0  1     |  2  3 |  4          |  5  6  7 (24 octets)
        'dec 00 01 02 03 04 05 06 07   08      09 10 11   12 13 14 15   16 17       18 19   20            21 22 23 tabBIN
        '-----------------------------------------------------------------------------------------------------
        'hex fb 59 2f 56 d4 01 8f 8f | 1e    | 00 00 00 | 3f 00 00 00 | 1c 03     | 00 00 | 64          | 00 00 00
        'dec                           30                 0000003f      031c                100%
        '                                                 63/208        001 100 011 100
        '                                                 +0.30         2   e   4   e
        '                                                               e2e4

        Array.Copy(epdToEXP(entreeDefrag, sortieDefrag, positionEPD), 0, tabBIN, 0, 8) '0-7

        tabBIN(8) = prof '8

        tabBIN(9) = 0 '9
        tabBIN(10) = 0 'a
        tabBIN(11) = 0 'b

        'score +0.30 => cp 30
        scoreCP = CInt(CInt(scoreCP) * 100 / facteur)
        'cp 30 => 63 => 3f
        Array.Copy(scoreToBIN(scoreCP), 0, tabBIN, 12, 2) 'c-d
        If scoreCP >= 0 Then
            tabBIN(14) = 0 'e
            tabBIN(15) = 0 'f
        Else
            tabBIN(14) = 255 'e
            tabBIN(15) = 255 'f
        End If

        'e2e4 => 0 000 001(2) 100(e) 011(4) 100(e)
        '03 1C => 1C 03
        '          0  1
        Array.Copy(moveToBIN(coupUCI), 0, tabBIN, 16, 2) '0-1

        tabBIN(18) = 0 '2
        tabBIN(19) = 0 '3

        tabBIN(20) = performance '4

        tabBIN(21) = 0 '5
        tabBIN(22) = 0 '6
        tabBIN(23) = 0 '7

    End Sub

    Public Function epdPieces(fen As String) As Integer
        fen = gauche(fen, fen.IndexOf(" "))
        fen = Replace(fen, "/", "")
        fen = Replace(fen, "1", "")
        fen = Replace(fen, "2", "")
        fen = Replace(fen, "3", "")
        fen = Replace(fen, "4", "")
        fen = Replace(fen, "5", "")
        fen = Replace(fen, "6", "")
        fen = Replace(fen, "7", "")
        fen = Replace(fen, "8", "")
        fen = Replace(fen, "r", "1", , , CompareMethod.Text)
        fen = Replace(fen, "n", "1", , , CompareMethod.Text)
        fen = Replace(fen, "b", "1", , , CompareMethod.Text)
        fen = Replace(fen, "q", "1", , , CompareMethod.Text)
        fen = Replace(fen, "k", "1", , , CompareMethod.Text)
        fen = Replace(fen, "p", "1", , , CompareMethod.Text)

        Return Len(fen)
    End Function

    Public Function epdToEXP(entreeDefrag As System.IO.StreamWriter, sortieDefrag As System.IO.StreamReader, Optional startpos As String = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1") As Byte()
        Dim key As String

        entreedefrag.WriteLine("position fen " & startpos)

        entreedefrag.WriteLine("d")

        key = ""
        While InStr(key, "Key: ", CompareMethod.Text) = 0
            key = sortiedefrag.ReadLine
        End While

        key = Replace(key, "Key: ", "")

        '6078880BD90221F8 =>  F8 21 02  D9 0B  88  78 60
        '                    248 33  2 217 11 136 120 96

        Return inverseurHEX(key, 8)

    End Function

    Public Function gauche(texte As String, longueur As Integer) As String
        If longueur > 0 Then
            Return VB.Left(texte, longueur)
        Else
            Return ""
        End If
    End Function

    Public Function heureFin(depart As Integer, i As Long, max As Long, Optional reprise As Long = 0, Optional formatCourt As Boolean = False) As String
        If formatCourt Then
            Return Format(DateAdd(DateInterval.Second, (max - i) * ((Environment.TickCount - depart) / 1000) / (i - reprise), Now), "dd/MM/yy HH:mm:ss")
        Else
            Return Format(DateAdd(DateInterval.Second, (max - i) * ((Environment.TickCount - depart) / 1000) / (i - reprise), Now), "dddd' 'd' 'MMM' @ 'HH'h'mm'm'ss")
        End If
    End Function

    Public Function inverseurHEX(chaine As String, taille As Integer) As Byte()
        Dim i As Integer, index As Integer, tab(taille - 1) As Byte

        index = 0
        For i = Len(chaine) To 2 Step -2
            tab(index) = Convert.ToInt64(droite(gauche(chaine, i), 2), 16)
            index = index + 1
        Next

        Return tab
    End Function

    Public Function moveToBIN(coup As String, Optional litteral As Boolean = False) As Byte()
        Dim coupBIN As String, i As Integer, cumul As Integer, coupHEX As String

        'g8h6
        '0 000 111(8) 110(g) 101(6) 111(h)

        coupBIN = ""

        If Not litteral Then
            If coup = "e1c1" Then
                coupBIN = "1 100 000 100 000 000" 'equivalent e1a1

            ElseIf coup = "e8c8" Then
                coupBIN = "1 100 111 100 111 000" 'equivalent e8a8

            ElseIf coup = "e1g1" Then
                coupBIN = "1 100 000 100 000 111" 'equivalent e1h1

            ElseIf coup = "e8g8" Then
                coupBIN = "1 100 111 100 111 111" 'equivalent e8h8

            End If
        End If

        If coupBIN = "" Then
            coupBIN = "0 000 "
            If Len(coup) = 5 Then
                Select Case droite(coup, 1)
                    Case "N", "n"
                        coupBIN = "0 001 "
                    Case "B", "b"
                        coupBIN = "0 101 "
                    Case "R", "r"
                        coupBIN = "0 110 "
                    Case "Q", "q"
                        coupBIN = "0 111 "
                End Select
            End If

            'ligne de départ
            If coup.Substring(1, 1) = "1" Then
                coupBIN = coupBIN & "000" & " "
            ElseIf coup.Substring(1, 1) = "2" Then
                coupBIN = coupBIN & "001" & " "
            ElseIf coup.Substring(1, 1) = "3" Then
                coupBIN = coupBIN & "010" & " "
            ElseIf coup.Substring(1, 1) = "4" Then
                coupBIN = coupBIN & "011" & " "
            ElseIf coup.Substring(1, 1) = "5" Then
                coupBIN = coupBIN & "100" & " "
            ElseIf coup.Substring(1, 1) = "6" Then
                coupBIN = coupBIN & "101" & " "
            ElseIf coup.Substring(1, 1) = "7" Then
                coupBIN = coupBIN & "110" & " "
            ElseIf coup.Substring(1, 1) = "8" Then
                coupBIN = coupBIN & "111" & " "
            End If

            'colonne de départ
            If coup.Substring(0, 1) = "a" Then
                coupBIN = coupBIN & "000" & " "
            ElseIf coup.Substring(0, 1) = "b" Then
                coupBIN = coupBIN & "001" & " "
            ElseIf coup.Substring(0, 1) = "c" Then
                coupBIN = coupBIN & "010" & " "
            ElseIf coup.Substring(0, 1) = "d" Then
                coupBIN = coupBIN & "011" & " "
            ElseIf coup.Substring(0, 1) = "e" Then
                coupBIN = coupBIN & "100" & " "
            ElseIf coup.Substring(0, 1) = "f" Then
                coupBIN = coupBIN & "101" & " "
            ElseIf coup.Substring(0, 1) = "g" Then
                coupBIN = coupBIN & "110" & " "
            ElseIf coup.Substring(0, 1) = "h" Then
                coupBIN = coupBIN & "111" & " "
            End If

            'ligne d'arrivée
            If coup.Substring(3, 1) = "1" Then
                coupBIN = coupBIN & "000" & " "
            ElseIf coup.Substring(3, 1) = "2" Then
                coupBIN = coupBIN & "001" & " "
            ElseIf coup.Substring(3, 1) = "3" Then
                coupBIN = coupBIN & "010" & " "
            ElseIf coup.Substring(3, 1) = "4" Then
                coupBIN = coupBIN & "011" & " "
            ElseIf coup.Substring(3, 1) = "5" Then
                coupBIN = coupBIN & "100" & " "
            ElseIf coup.Substring(3, 1) = "6" Then
                coupBIN = coupBIN & "101" & " "
            ElseIf coup.Substring(3, 1) = "7" Then
                coupBIN = coupBIN & "110" & " "
            ElseIf coup.Substring(3, 1) = "8" Then
                coupBIN = coupBIN & "111" & " "
            End If

            'colonne d'arrivée
            If coup.Substring(2, 1) = "a" Then
                coupBIN = coupBIN & "000"
            ElseIf coup.Substring(2, 1) = "b" Then
                coupBIN = coupBIN & "001"
            ElseIf coup.Substring(2, 1) = "c" Then
                coupBIN = coupBIN & "010"
            ElseIf coup.Substring(2, 1) = "d" Then
                coupBIN = coupBIN & "011"
            ElseIf coup.Substring(2, 1) = "e" Then
                coupBIN = coupBIN & "100"
            ElseIf coup.Substring(2, 1) = "f" Then
                coupBIN = coupBIN & "101"
            ElseIf coup.Substring(2, 1) = "g" Then
                coupBIN = coupBIN & "110"
            ElseIf coup.Substring(2, 1) = "h" Then
                coupBIN = coupBIN & "111"
            End If
        End If

        '0 000 111(8) 110(g) 101(6) 111(h)
        coupBIN = Replace(coupBIN, " ", "")

        '0000111110101111
        cumul = 0
        For i = 1 To Len(coupBIN)
            cumul = cumul + CInt(gauche(droite(coupBIN, i), 1)) * 2 ^ (i - 1)
        Next
        coupHEX = Hex(cumul)
        coupHEX = StrDup(CInt(Len(coupBIN) / 4 - Len(coupHEX)), "0") & coupHEX

        '0000(0) 1111(F) 1010(A) 1111(F)
        'AF(175) OF(15)

        Return inverseurHEX(coupHEX, 2)
    End Function

    Public Function nomFichier(chemin As String) As String
        Return My.Computer.FileSystem.GetName(chemin)
    End Function

    Public Function scoreToBIN(eval As Integer) As Byte()
        Dim tab(1) As Byte, scoreHEX As String

        scoreHEX = ""
        If eval >= 0 Then
            scoreHEX = Hex(eval * 2.08)
        Else
            scoreHEX = Hex(eval * 2.08 + 65535)
        End If
        scoreHEX = StrDup(4 - Len(scoreHEX), "0") & scoreHEX

        Return inverseurHEX(scoreHEX, 2)
    End Function


End Module
