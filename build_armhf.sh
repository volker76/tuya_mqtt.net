#!/bin/bash

git pull
branch=$(git rev-parse --abbrev-ref HEAD)
subbranch="$branch-"

image="volkerhaensel/tuya_mqtt.net"

if [ "$branch" == "main" ] || [ "$branch" == "master" ]
then
  branch='latest'
  subbranch=''
fi

tags=($(git tag -l))

arch=$(uname -m)

if [ "$arch" == "armhf" ] || [ "$arch" == "armv7l" ]
then
  
  docker build tuya_mqtt.net -t $image:arm32-$branch

  image_ID=$(docker images --format="{{.Repository}}:{{.Tag}} {{.ID}}" | grep $image:arm32-$branch  | cut -d' ' -f2 )     

  echo "image $image_ID is used"

  echo "run docker push $image:arm32-$branch"

  docker push $image:arm32-$branch

  for i in "${tags[@]}"
  do
   echo "current version have tag $i"
   echo "run  docker tag $image_ID $image:arm32-$subbranch$i"  
   docker tag $image_ID $image:arm32-$subbranch$i  
   echo "run docker push $image:arm32-$subbranch$i"
   docker push $image:arm32-$subbranch$i  

  done


else
  echo "architecture $arch found"
  echo "this build script is designed to run on arm32 bit."  
  echo "exiting."
fi
 
