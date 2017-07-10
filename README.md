# CodeAtlasVsix
Call graph visualization plugin of Visual Studio 2015

Code Atlas is a plugin of Visual Studio, which allows one to explore the call graph conveniently. The plugin uses doxygen to perform symbol/reference query task.

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

![](https://github.com/league1991/CodeAtlasSublime/raw/master/githubCache/main.png) 

Navigate
--------

Move cursor onto function/class/variable name in Sublime Text Editor, then press `Alt+G` to show it on CodeAtlas.

Press `Alt+Up/Down/Left/Right` in Sublime Text to jump to neighbour items.
![](https://github.com/league1991/CodeAtlasSublime/raw/master/githubCache/navigate.gif)  

Find Callers/Callees
--------------------

Press `Alt+C/V` to find callers or callees.
![](https://github.com/league1991/CodeAtlasSublime/raw/master/githubCache/call.gif)   

Find Class Hierarchy
--------------------

Press `Alt+B` to find base and derived class.
![](https://github.com/league1991/CodeAtlasSublime/raw/master/githubCache/class.gif)  
 
Find Overloaded Functions
-------------------------

Press `Alt+O` to find overloaded functions.
![](https://github.com/league1991/CodeAtlasSublime/raw/master/githubCache/overload.gif)  
 
Find Class Member
-----------------

Press `Alt+M` to find all class variables and the largest member function. 
Press `Alt+M` several times to see smaller member functions.
![](https://github.com/league1991/CodeAtlasSublime/raw/master/githubCache/member.gif)  
 
Find Variable Usage
-------------------

Press `Alt+U` to find all functions that use selected variable.
![](https://github.com/league1991/CodeAtlasSublime/raw/master/githubCache/usage.gif)  
 
Save / Load Relationship Graph
------------------------------

Press `Ctrl+Num` to add selected edge to a relationship graph.
![](https://github.com/league1991/CodeAtlasSublime/raw/master/githubCache/addGraph.gif)  

Press `Alt+Num` to show relationship graph listed at the top left corner.
![](https://github.com/league1991/CodeAtlasSublime/raw/master/githubCache/graph.gif)  

Add Comment
------------------------------

Input your comment for functions/classes/variables in Symbol panel.
![](https://github.com/league1991/CodeAtlasSublime/raw/master/githubCache/comment.gif) 

