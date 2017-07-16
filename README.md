# CodeAtlasVsix
Call graph visualization plugin of Visual Studio 2010-2015

Code Atlas is a plugin of Visual Studio. It allows one to explore the call graph conveniently. The plugin uses doxygen to parse code.

Supported languages(not limited): 
C/C++(Tested)
Python,Java,C#(Not Tested yet)

Using this plugin one can navigate the code easily.

Here is my blog:
http://www.cnblogs.com/dydx/p/6299927.html

Here are some introduction videos:
https://www.youtube.com/watch?v=FScdHyxdNFw&list=PLN16zMWJLkHLgHhTJUIkwp5chgnFz9_NH

Overview
--------
* **Square** is a class.
* **Disc** is a function.
* **Triangle** is a variable.
* Colors for these shapes represent different classes, whose name can be seen at the bottom-left corner.
* Colors for edges represent different graphs, whose name and key short-cut can be seen at the top-left corner.

![](https://github.com/league1991/CodeAtlasVsix/raw/master/githubCache/main.png) 

Navigate
--------

Move cursor onto function/class/variable name in Visual Studio Text Editor, then press `Alt+G` to show it on Code Atlas.

Press `Alt+Up/Down/Left/Right` in Visual Studio Text Editor to jump to neighbour items.
![](https://github.com/league1991/CodeAtlasVsix/raw/master/githubCache/navigate.gif)  

Find Callers/Callees
--------------------

Press `Alt+C/V` to find callers or callees.
![](https://github.com/league1991/CodeAtlasVsix/raw/master/githubCache/call.gif)   

Find Class Hierarchy
--------------------

Press `Alt+B` to find base and derived class.
![](https://github.com/league1991/CodeAtlasVsix/raw/master/githubCache/class.gif)  
 
Find Overloaded Functions
-------------------------

Press `Alt+O` to find overloaded functions.
![](https://github.com/league1991/CodeAtlasVsix/raw/master/githubCache/overload.gif)  
 
Find Class Member
-----------------

Press `Alt+M` to find one class variable and the largest member function. 
Press `Alt+M` several times to see more members.
![](https://github.com/league1991/CodeAtlasVsix/raw/master/githubCache/member.gif)  
 
Find Variable Usage
-------------------

Press `Alt+U` to find all functions that use selected variable.
![](https://github.com/league1991/CodeAtlasVsix/raw/master/githubCache/usage.gif)  
 
Save / Load Relationship Graph
------------------------------

Press `Ctrl+Num` to add selected edge to a relationship graph.
![](https://github.com/league1991/CodeAtlasVsix/raw/master/githubCache/addGraph.gif)  

Press `Alt+Num` to show relationship graph listed at the bottom left corner.
![](https://github.com/league1991/CodeAtlasVsix/raw/master/githubCache/graph.gif)  

Add Comment
------------------------------

Input your comment for functions/classes/variables in Symbol panel.
![](https://github.com/league1991/CodeAtlasVsix/raw/master/githubCache/comment.gif) 

Setup
--------

1. Download the vsix package or compile one yourself.

![](https://github.com/league1991/CodeAtlasVsix/raw/master/githubCache/setup1.png) 

2. Open and install the package.

![](https://github.com/league1991/CodeAtlasVsix/raw/master/githubCache/setup2.png) 

3. The package is installed.

![](https://github.com/league1991/CodeAtlasVsix/raw/master/githubCache/setup3.png) 

4. Open Visual Studio, you can see a menu named "Code Atlas". Press "Open Code Atlas Window" to show the window.

![](https://github.com/league1991/CodeAtlasVsix/raw/master/githubCache/setup4.png) 

5. Open a solution, then press "Analyse solution" to generate the code database for Code Atlas.

![](https://github.com/league1991/CodeAtlasVsix/raw/master/githubCache/setup5.png) 

6. A command-line window appears, and you can see "Wait..." on the viewport. **You can continue coding during analysis process**.

![](https://github.com/league1991/CodeAtlasVsix/raw/master/githubCache/setup6.png) 

7. When analysis is completed, the code database will be opened automatically. Place the cursor on a variable/function/class and press `Alt+G` , then the symbol will appear in the viewport.

![](https://github.com/league1991/CodeAtlasVsix/raw/master/githubCache/setup7.png) 

8. Next time when you open Visual Studio, you don't have to analyse the solution again, just click "Open Analysis Result" and choose a ".atlas" file. The file with a "solution" postfix is the analysis result (code database) for the whole solution.

![](https://github.com/league1991/CodeAtlasVsix/raw/master/githubCache/setup8.png) 
