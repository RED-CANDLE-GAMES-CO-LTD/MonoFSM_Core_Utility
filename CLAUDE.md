* 如果我沒有選擇程式碼片段(select line)或沒有提供目前編輯的檔案時，先提醒我
* 當我提出需求時，先回應我清楚度 1-10分，當問題不清楚時(<7)，請要求我提供更多資訊
* 改完一個檔案後，要先用getDiagnostics檢查有沒有錯誤，記得檔案要用絕對路徑，遇到 "File not found" 要試不同的路徑格式看看，不可略過
* 此專案使用Odin Inspector, 編輯器工具盡量使用已有的Attribute (已搭配AttributeDrawer)
  * ex: 1_MonoFSM_Core/Runtime/Attributes/CompRefAttribute.cs