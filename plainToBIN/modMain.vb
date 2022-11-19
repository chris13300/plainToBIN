Imports System.IO
Imports System.Text

Module modMain

    Public moteurBIN As String
    Public moteur_court As String
    Public fichierBIN As String

    Sub Main()
        Dim lecture As TextReader, fichierPLAIN As String, fichierINI As String
        Dim chaine As String, tabChaine() As String, ligne As String, tabTmp() As String
        Dim positionEPD As String, coup As String, prof As Integer, scoreCP As Integer, facteur As Integer
        Dim compteur As Integer, indexTampon As Integer
        Dim nbPositions As Integer, depart As Integer, cumul As Long, tailleFichier As Long
        Dim tabBIN(0) As Byte, tabTampon(0) As Byte
        Dim reponse As String, nbRejets As Integer, minPieces As Integer
        
        fichierPLAIN = Replace(Command(), """", "")
        If fichierPLAIN = "" Then
            End
        End If
        Try
            tailleFichier = FileLen(fichierPLAIN)
        Catch ex As Exception
            End
        End Try

        fichierBIN = Replace(fichierPLAIN, "_plain.txt", "_exp.bin")
        If My.Computer.FileSystem.FileExists(fichierBIN) Then
            If MsgBox("The " & nomFichier(fichierBIN) & " file already exists." & vbCrLf & "Do you want to delete it ?", MsgBoxStyle.YesNo) = MsgBoxResult.Yes Then
                My.Computer.FileSystem.DeleteFile(fichierBIN)
            End If
        End If

        'chargement parametres
        moteurBIN = "BrainLearn.exe"
        fichierINI = My.Computer.Name & ".ini"
        If My.Computer.FileSystem.FileExists(fichierINI) Then
            chaine = My.Computer.FileSystem.ReadAllText(fichierINI)
            If chaine <> "" And InStr(chaine, vbCrLf) > 0 Then
                tabChaine = Split(chaine, vbCrLf)
                For i = 0 To UBound(tabChaine)
                    If tabChaine(i) <> "" And InStr(tabChaine(i), " = ") > 0 Then
                        tabTmp = Split(tabChaine(i), " = ")
                        If tabTmp(0) <> "" And tabTmp(1) <> "" Then
                            If InStr(tabTmp(1), "//") > 0 Then
                                tabTmp(1) = Trim(gauche(tabTmp(1), tabTmp(1).IndexOf("//") - 1))
                            End If
                            Select Case tabTmp(0)
                                Case "moteurBIN"
                                    moteurBIN = tabTmp(1)
                                Case Else

                            End Select
                        End If
                    End If
                Next
            End If
        End If
        My.Computer.FileSystem.WriteAllText(fichierINI, "moteurBIN = " & moteurBIN & vbCrLf, False)

        reponse = InputBox("What was the depth of analysis ?", nomFichier(fichierPLAIN), "14")
        If reponse = "" Then
            End
        End If
        prof = CInt(reponse)
        If Not IsNumeric(prof) Then
            End
        End If

        reponse = InputBox("What was the score's factor ?", nomFichier(fichierPLAIN), "208")
        If reponse = "" Then
            End
        End If
        facteur = CInt(reponse)
        If Not IsNumeric(facteur) Then
            End
        End If

        reponse = InputBox("At least how many pieces should the position contain ?", nomFichier(fichierPLAIN), "22")
        If reponse = "" Then
            End
        End If
        minPieces = CInt(reponse)
        If Not IsNumeric(minPieces) Then
            End
        End If

        lecture = My.Computer.FileSystem.OpenTextFileReader(fichierPLAIN, New System.Text.UTF8Encoding(False))

        moteur_court = nomFichier(moteurBIN)

        Console.Write("Loading " & moteur_court & "... ")
        chargerMoteur(moteurBIN)
        Console.WriteLine("OK")

        positionEPD = ""
        coup = ""
        scoreCP = 0
        'stats
        nbPositions = 0
        nbRejets = 0
        depart = Environment.TickCount
        cumul = 0
        indexTampon = 0

        Console.Write("Conversion " & nomFichier(fichierPLAIN) & "... ")
        ReDim tabBIN(23)
        ReDim tabTampon(1199999) '50000 * 24
        Do
            'fen rnbqkbnr/2pppppp/1p5B/p7/3P4/P7/1PP1PPPP/RN1QKBNR b KQkq - 1 3
            'move g8h6
            'score 936
            'ply 5
            'result 0
            'e
            ligne = lecture.ReadLine()
            cumul = cumul + Len(ligne) + 2

            If gauche(ligne, 3) = "fen" Then
                'fen rnbqkbnr/2pppppp/1p5B/p7/3P4/P7/1PP1PPPP/RN1QKBNR b KQkq - 1 3
                'rnbqkbnr/2pppppp/1p5B/p7/3P4/P7/1PP1PPPP/RN1QKBNR b KQkq - 1 3
                positionEPD = Replace(ligne, "fen ", "")
            ElseIf gauche(ligne, 4) = "move" Then
                'move g8h6
                'g8h6
                coup = Replace(ligne, "move ", "")
            ElseIf gauche(ligne, 5) = "score" Then
                'score 936
                '936
                scoreCP = CInt(Replace(ligne, "score ", ""))
            ElseIf ligne = "e" Then
                If minPieces <= epdPieces(positionEPD) Then 'on limite par le nombre de pièces de la position
                    entreeBIN(tabBIN, positionEPD, coup, scoreCP, facteur, prof, 100, entree, sortie)

                    'stats
                    nbPositions = nbPositions + 1
                    If indexTampon + 24 <= tabTampon.Length Then
                        Array.Copy(tabBIN, 0, tabTampon, indexTampon, 24)
                        indexTampon = indexTampon + 24
                    Else
                        'on vide le tampon dans le fichierBIN
                        My.Computer.FileSystem.WriteAllBytes(fichierBIN, tabTampon, True)
                        Array.Clear(tabTampon, 0, tabTampon.Length)
                        indexTampon = 0
                    End If
                Else
                    nbRejets = nbRejets + 1
                End If

                If (nbPositions + nbRejets) Mod 50000 = 0 Then
                    Console.Clear()
                    Console.Title = My.Computer.Name & " : Conversion @ " & Format(cumul / tailleFichier, "0.00%") & " (" & heureFin(depart, cumul, tailleFichier, , True) & ")"
                    Console.WriteLine("Moves  : " & Trim(Format(nbPositions, "# ### ### ##0")))
                    Console.WriteLine("Reject : " & Trim(Format(nbRejets, "# ### ### ##0")))
                    Console.WriteLine("Rate   : " & Trim(Format(nbPositions / (Environment.TickCount - depart), "# ### ### ##0 pos/ms")))
                End If

                'nettoyage
                positionEPD = ""
                coup = ""
                scoreCP = 0
                compteur = 0
                Array.Clear(tabBIN, 0, tabBIN.Length)
            End If
        Loop Until ligne Is Nothing
        lecture.Close()

        If indexTampon > 0 Then
            'on vide le tampon dans le fichierBIN
            ReDim Preserve tabTampon(indexTampon - 1)
            My.Computer.FileSystem.WriteAllBytes(fichierBIN, tabTampon, True)
        End If
        Console.WriteLine("OK")

        Console.Clear()
        Console.Title = My.Computer.Name & " : Conversion @ " & Format(cumul / tailleFichier, "0%")
        Console.WriteLine("Moves  : " & Trim(Format(nbPositions, "# ### ### ##0")))
        Console.WriteLine("Reject : " & Trim(Format(nbRejets, "# ### ### ##0")))
        Console.WriteLine("Rate   : " & Trim(Format(nbPositions / (Environment.TickCount - depart), "# ### ### ##0")) & " moves/ms")

        Console.WriteLine("Defragging...")

        Console.WriteLine(defragBIN(fichierBIN, 1))

        dechargerMoteur()

        Console.WriteLine("Press ENTER to close this window.")
        Console.ReadLine()

    End Sub

End Module
